---Executables that PlusPim provides, keyed by purpose.
local M = {}

---@alias pluspim.Purpose "dap"

---@type table<pluspim.Purpose, string>
M.executables = { dap = "pluspim" }

---Finds the executable for `purpose`, trying `user_path` first and then `PATH`.
---@param purpose pluspim.Purpose
---@param user_path? string path given in `setup({ paths = { [purpose] = ... } })`
---@return string? path absolute path of the executable
---@return string? err message naming the purpose and the places searched
function M.resolve(purpose, user_path)
  local name = M.executables[purpose]
  local candidates = user_path and { vim.fs.normalize(user_path), name } or { name }
  for _, candidate in ipairs(candidates) do
    local path = vim.fn.exepath(candidate)
    if path ~= "" then
      return path
    end
  end
  local places = user_path and ("%s, then `%s` on PATH"):format(candidates[1], name) or ("`%s` on PATH"):format(name)
  return nil, ("PlusPim: executable for %s not found (searched %s)"):format(purpose, places)
end

return M
