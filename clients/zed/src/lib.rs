//! Zed extension for PlusPim, a MIPS emulator and debugger.

mod dap;
mod tool;

use zed_extension_api::{
    self as zed, DebugAdapterBinary, DebugConfig, DebugScenario, DebugTaskDefinition, Result,
    StartDebuggingRequestArgumentsRequest, Worktree, serde_json::Value,
};

struct PlusPim;

impl zed::Extension for PlusPim {
    fn new() -> Self {
        Self
    }

    fn get_dap_binary(
        &mut self,
        _adapter: String,
        config: DebugTaskDefinition,
        configured: Option<String>,
        worktree: &Worktree,
    ) -> Result<DebugAdapterBinary> {
        dap::binary(config, configured, worktree)
    }

    fn dap_request_kind(
        &mut self,
        _adapter: String,
        config: Value,
    ) -> Result<StartDebuggingRequestArgumentsRequest> {
        dap::request_kind(&config)
    }

    fn dap_config_to_scenario(&mut self, config: DebugConfig) -> Result<DebugScenario> {
        dap::scenario(config)
    }
}

zed::register_extension!(PlusPim);
