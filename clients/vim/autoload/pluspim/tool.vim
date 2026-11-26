vim9script
# Executables PlusPim provides.

# Executable name per purpose. LSP support adds one row here.
const TOOLS = {dap: 'pluspim'}

# Returns the full path of the executable for {purpose}.
# Tries g:pluspim_paths[{purpose}] first, then the executable name on $PATH.
# Throws "PlusPim: ..." naming the purpose and both places when neither is executable.
export def Resolve(purpose: string): string
  const configured = get(g:, 'pluspim_paths', {})->get(purpose, '')
  # exepath() rejects an empty name, so unset entries are dropped first.
  const found = [configured, TOOLS[purpose]]
    ->filter((_, c) => c != '')
    ->map((_, c) => exepath(expand(c)))
    ->filter((_, p) => p != '')
  if empty(found)
    throw $'PlusPim: no executable for {purpose} (g:pluspim_paths.{purpose} = {string(configured)}, {TOOLS[purpose]} on $PATH)'
  endif
  return found[0]
enddef
