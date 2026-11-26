//! The executables PlusPim ships, one per editor feature.

use zed_extension_api::{Result, Worktree};

/// A PlusPim executable, named by the feature it serves.
#[derive(Clone, Copy)]
pub enum Tool {
    /// The debug adapter.
    Dap,
}

/// Where a [`Tool`] is found and how it is described to the user.
struct Entry {
    purpose: &'static str,
    /// Program name searched on `PATH`.
    executable: &'static str,
    /// Setting whose value overrides the `PATH` search.
    setting: &'static str,
}

impl Tool {
    const fn entry(self) -> Entry {
        match self {
            Self::Dap => Entry {
                purpose: "debug adapter",
                executable: "pluspim",
                setting: "dap.pluspim.binary",
            },
        }
    }

    /// Returns the path to run, preferring `configured` (the user's setting) over `PATH`.
    pub fn resolve(self, configured: Option<String>, worktree: &Worktree) -> Result<String> {
        let Entry {
            purpose,
            executable,
            setting,
        } = self.entry();
        configured.or_else(|| worktree.which(executable)).ok_or_else(|| {
            format!("PlusPim {purpose} `{executable}` not found: set `{setting}` in settings.json or add it to PATH")
        })
    }
}
