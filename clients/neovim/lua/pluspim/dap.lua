---nvim-dap integration. PlusPim runs in a terminal buffer so the debuggee owns its stdio,
---and nvim-dap connects over TCP to the address PlusPim announces there.
local tools = require("pluspim.tools")

local M = {}

---@class pluspim.LaunchConfig
---@field program string|string[] assembly files PlusPim reads from `launch`
---@field args? string[] extra command-line options such as `--verbose`
---@field forceTerminalUtf8? boolean defaults to true
---@field cwd? string base of a relative `program`; defaults to the current directory
---@field stopOnEntry? boolean read by PlusPim from `launch`; defaults to true

---Terminal of the latest session. Kept after the session ends so its output stays readable.
---@type { buf: integer, job: integer }?
local terminal

---@param exe string
---@param config pluspim.LaunchConfig
---@return string[]
local function command(exe, config)
  local cmd = { exe, "debug", "listen", "127.0.0.1:0" }
  if config.forceTerminalUtf8 ~= false then
    table.insert(cmd, "--force-terminal-utf8")
  end
  return vim.list_extend(cmd, config.args or {})
end

---Returns an `on_stdout` handler that calls `on_address` once with the announced address.
---@param on_address fun(host: string, port: integer)
---@return fun(job: integer, data: string[])
function M.announce_parser(on_address)
  ---@type string? stream received so far; nil once the address is found
  local seen = ""
  return function(_, data)
    if not seen then
      return
    end
    -- data[1] continues the previous chunk's last line, so joining rebuilds the stream.
    seen = seen .. table.concat(data, "\n")
    -- Requiring trailing whitespace keeps a port split across chunks from being read early.
    local host, port = seen:match("DAP server listening at: (%S+):(%d+)%s")
    if host then
      seen = nil
      on_address(host, assert(tonumber(port)))
    end
  end
end

---nvim-dap adapter: starts PlusPim in a terminal split and hands nvim-dap its TCP address.
---@param callback fun(adapter: table)
---@param config pluspim.LaunchConfig
---@param user_path? string
local function start(callback, config, user_path)
  local exe, err = tools.resolve("dap", user_path)
  if not exe then
    return vim.notify(err, vim.log.levels.ERROR)
  end
  if terminal and vim.api.nvim_buf_is_valid(terminal.buf) then
    vim.api.nvim_buf_delete(terminal.buf, { force = true })
  end
  vim.cmd("botright new")
  local found = false
  local job = vim.fn.jobstart(command(exe, config), {
    term = true,
    on_stdout = M.announce_parser(function(host, port)
      found = true
      callback({
        type = "server",
        host = host,
        port = port,
        -- PlusPim rejects a relative `program` unless `cwd` is absolute.
        enrich_config = function(c, on_config)
          on_config(vim.tbl_extend("keep", c, { cwd = vim.fn.getcwd() }))
        end,
      })
    end),
    on_exit = function(_, code)
      if not found then
        vim.notify(("PlusPim exited with code %d before listening; see its terminal"):format(code), vim.log.levels.ERROR)
      end
    end,
  })
  terminal = { buf = vim.api.nvim_get_current_buf(), job = job }
  vim.cmd.wincmd("p")
end

---@param user_path? string
function M.setup(user_path)
  local dap = require("dap")
  dap.adapters.pluspim = function(callback, config)
    start(callback, config, user_path)
  end
  dap.configurations.mips = dap.configurations.mips
    or {
      {
        type = "pluspim",
        request = "launch",
        name = "PlusPim: current file",
        program = "${file}",
        stopOnEntry = true,
        forceTerminalUtf8 = true,
      },
    }
  -- PlusPim keeps running after `terminated` until it is stopped.
  for _, event in ipairs({ "event_terminated", "disconnect" }) do
    dap.listeners.after[event].pluspim = function(session)
      if terminal and session.config.type == "pluspim" then
        vim.fn.jobstop(terminal.job)
      end
    end
  end
end

return M
