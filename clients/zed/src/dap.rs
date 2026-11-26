//! The `pluspim` debug adapter.
//!
//! PlusPim speaks DAP over TCP only, because the debuggee's stdin and stdout are PlusPim's own.
//! A scenario with `tcp_connection` reaches a PlusPim that the user already runs in a Zed
//! terminal, where the debuggee's I/O is interactive. Without one, Zed spawns PlusPim itself.

use std::net::Ipv4Addr;

use zed_extension_api::{
    DebugAdapterBinary, DebugConfig, DebugRequest, DebugScenario, DebugTaskDefinition, Result,
    StartDebuggingRequestArguments, StartDebuggingRequestArgumentsRequest, TcpArgumentsTemplate,
    Worktree, resolve_tcp_template,
    serde_json::{self, Value, json},
};

use crate::tool::Tool;

/// Reads a string or an array of strings.
fn strings(value: &Value) -> Option<Vec<String>> {
    match value {
        Value::String(s) => Some(vec![s.clone()]),
        Value::Array(items) => items
            .iter()
            .map(|v| v.as_str().map(str::to_owned))
            .collect(),
        _ => None,
    }
}

/// Returns how Zed starts or reaches PlusPim for `definition`.
///
/// Either way PlusPim loads `program` on `launch`. A scenario with `tcp_connection` reaches a
/// PlusPim that already listens there; otherwise Zed spawns `debug listen <ip:port> <args>`.
pub fn binary(
    definition: DebugTaskDefinition,
    configured: Option<String>,
    worktree: &Worktree,
) -> Result<DebugAdapterBinary> {
    let mut config: Value =
        serde_json::from_str(&definition.config).map_err(|e| e.to_string())?;
    strings(&config["program"])
        .filter(|p| !p.is_empty())
        .ok_or("`program` must be a path or a non-empty list of paths")?;
    // PlusPim rejects a relative `program` unless `cwd` is absolute.
    if config["cwd"].is_null() {
        config["cwd"] = worktree.root_path().into();
    }
    let (command, arguments, cwd, connection) = match definition.tcp_connection {
        Some(tcp) if tcp.port.is_none() => {
            return Err("`tcp_connection.port` must be the port PlusPim listens on".into());
        }
        Some(tcp) => (None, Vec::new(), None, resolve_tcp_template(tcp)?),
        None => {
            let extra = match &config["args"] {
                Value::Null => Vec::new(),
                args => strings(args).ok_or("`args` must be a list of strings")?,
            };
            let tcp = resolve_tcp_template(TcpArgumentsTemplate {
                port: None,
                host: None,
                timeout: None,
            })?;
            let listen = format!("{}:{}", Ipv4Addr::from(tcp.host), tcp.port);
            let arguments = ["debug".into(), "listen".into(), listen]
                .into_iter()
                .chain(extra)
                .collect();
            (
                Some(Tool::Dap.resolve(configured, worktree)?),
                arguments,
                config["cwd"].as_str().map(str::to_owned),
                tcp,
            )
        }
    };
    Ok(DebugAdapterBinary {
        command,
        arguments,
        envs: Vec::new(),
        cwd,
        connection: Some(connection),
        request_args: StartDebuggingRequestArguments {
            configuration: config.to_string(),
            request: StartDebuggingRequestArgumentsRequest::Launch,
        },
    })
}

/// Accepts only `launch`, the one request PlusPim implements.
pub fn request_kind(config: &Value) -> Result<StartDebuggingRequestArgumentsRequest> {
    match config.get("request") {
        None => Ok(StartDebuggingRequestArgumentsRequest::Launch),
        Some(request) if request == "launch" => Ok(StartDebuggingRequestArgumentsRequest::Launch),
        Some(_) => Err("PlusPim supports only `\"request\": \"launch\"`".into()),
    }
}

/// Turns a program chosen in the new process modal into a scenario that spawns PlusPim.
pub fn scenario(config: DebugConfig) -> Result<DebugScenario> {
    let DebugRequest::Launch(launch) = config.request else {
        return Err(
            "PlusPim cannot attach to a process; use `tcp_connection` in debug.json".into(),
        );
    };
    let mut settings = json!({
        "request": "launch",
        "program": [launch.program],
        "args": launch.args,
    });
    if let Some(cwd) = launch.cwd {
        settings["cwd"] = cwd.into();
    }
    if let Some(stop) = config.stop_on_entry {
        settings["stopOnEntry"] = stop.into();
    }
    Ok(DebugScenario {
        label: config.label,
        adapter: config.adapter,
        build: None,
        config: settings.to_string(),
        tcp_connection: None,
    })
}
