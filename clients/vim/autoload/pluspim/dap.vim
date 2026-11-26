vim9script
# Debug sessions: PlusPim listens on TCP inside a Vim terminal and vimspector attaches to it.
# The debuggee's standard I/O stays in that terminal.

import autoload 'pluspim/tool.vim'

# The address announcement. The trailing non-digit keeps a chunk boundary from truncating the port.
const ANNOUNCE = 'DAP server listening at: \zs[0-9.]\+:\d\+\ze\D'
# CSI and OSC sequences a pty may interleave with the announcement.
const ESCAPES = "\e\\[[0-9;?]*[ -/]*[@-~]\\|\e\\][^\x07]*\x07"

# Starts PlusPim on {files} (the current file when empty) and attaches vimspector once it listens.
export def Start(files: list<string>)
  var exe: string
  try
    exe = tool.Resolve('dap')
  catch /^PlusPim:/
    Error(v:exception)
    return
  endtry
  const paths = (empty(files) ? [expand('%')] : files)->mapnew((_, f) => fnamemodify(f, ':p'))

  # Raw output before the announcement. Escapes are stripped from the whole text so one split across chunks still goes.
  var received = ''
  var attached = false
  var buf = 0
  def OnOutput(_: channel, msg: string)
    if attached
      return
    endif
    received ..= msg
    const addr = received->substitute(ESCAPES, '', 'g')->matchstr(ANNOUNCE)
    if addr != ''
      attached = true
      Attach(addr, buf, paths)
    endif
  enddef
  def OnExit(_: job, status: number)
    if !attached
      Error($'PlusPim: exited with status {status} before listening; see its terminal')
    endif
  enddef

  buf = term_start([exe, 'debug', 'listen', '127.0.0.1:0', '--force-terminal-utf8'], {
    term_name: 'PlusPim: ' .. paths->mapnew((_, p) => fnamemodify(p, ':t'))->join(', '),
    out_cb: OnOutput,
    exit_cb: OnExit,
  })
enddef

# Attaches vimspector to PlusPim at {addr} ("host:port") running in terminal {buf} and launches {paths}.
def Attach(addr: string, buf: number, paths: list<string>)
  const [host, port] = split(addr, ':')
  autocmd_add([
    # vimspector debugs in its own tab; show the debuggee's terminal there too.
    {event: 'User', pattern: 'VimspectorUICreated', once: true, cmd: $'botright sbuffer {buf}'},
    # The terminal buffer stays so its output can still be read.
    {event: 'User', pattern: 'VimspectorDebugEnded', once: true, cmd: $'call job_stop(term_getjob({buf}))'},
  ])
  vimspector#LaunchWithConfigurations({PlusPim: {
    adapter: {host: host, port: port},
    # vimspector asks about each exception filter it has no value for; '' takes PlusPim's default.
    breakpoints: {exception: {fatal: '', break: '', syscall: ''}},
    configuration: {request: 'launch', program: paths, stopOnEntry: true},
  }})
enddef

def Error(msg: string)
  echohl ErrorMsg
  echomsg msg
  echohl None
enddef
