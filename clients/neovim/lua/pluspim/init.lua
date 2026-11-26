local M = {}

---@class pluspim.Opts
---@field paths? table<pluspim.Purpose, string> executable path per purpose; unset ones are searched on PATH

---Registers the PlusPim adapter and the default `mips` configuration with nvim-dap.
---@param opts? pluspim.Opts
function M.setup(opts)
  local paths = (opts or {}).paths or {}
  require("pluspim.dap").setup(paths.dap)
end

return M
