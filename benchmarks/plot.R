# v0.5.0 と現在のバージョンのベンチマーク結果を並べて描く
#
# 使い方: リポジトリの根で Rscript benchmarks/plot.R
# 入力:
#   BenchmarkDotNet.Artifacts/results/   現在のバージョンの History と Dap (Dap は両バージョンを含む)
#   BenchmarkDotNet.Artifacts/v0.5.0/    v0.5.0 の worktree からコピーした History
# 出力: BenchmarkDotNet.Artifacts/comparison/*.png と標準出力の表
library(ggplot2)

root <- "BenchmarkDotNet.Artifacts"
out <- file.path(root, "comparison")
dir.create(out, showWarnings = FALSE)
versions <- c("v0.5.0", "current")

# BDN 0.15.8 の BuildPlots.R は dplyr 1.2 で廃止された group_by_() を使うので，置き換えて各ディレクトリの単体のグラフを描き直す
for (dir in c("results", "v0.5.0")) {
  script <- sub('group_by_(.dots = c("Target_Method", "Job_Id"))', "group_by(Target_Method, Job_Id)",
                readLines(file.path(root, dir, "BuildPlots.R"), encoding = "UTF-8"), fixed = TRUE)
  owd <- setwd(file.path(root, dir))
  eval(parse(text = script), new.env())
  setwd(owd)
}

# 本計測の反復だけを ms で読む
measurements <- function(dir, name, version = NA) {
  d <- read.csv(file.path(root, dir, paste0(name, "-measurements.csv")), check.names = FALSE)
  d <- d[d$Measurement_IterationMode == "Workload" & d$Measurement_IterationStage == "Result", ]
  data.frame(
    Method = d$Target_Method,
    Params = d$Params,
    Version = if (is.na(version)) sub(".*Version=([^,]+).*", "\\1", d$Params) else version,
    Ms = d$Measurement_Value / d$Measurement_Operations / 1e6
  )
}

samples <- function(dir, name, version = NA) {
  d <- read.csv(file.path(root, dir, paste0(name, "-samples.csv")))
  data.frame(
    Param = sub(".*/", "", d$Key),
    Version = if (is.na(version)) sub(".*/", "", d$Key) else version,
    Value = d$Value
  )
}

with_n <- function(d) {
  d$N <- as.integer(sub(".*N=([0-9]+).*", "\\1", d$Params))
  d$Version <- factor(d$Version, versions)
  d
}

save <- function(p, name, w = 7, h = 4.5) {
  ggsave(file.path(out, name), p, width = w, height = h, dpi = 150)
}

# 起動時間と fact の利用シナリオ
dap <- measurements("results", "Dap")
dap$Version <- factor(dap$Version, versions)
save(ggplot(dap, aes(Version, Ms, fill = Version)) +
  geom_boxplot(outlier.shape = NA) + geom_jitter(width = 0.15, size = 1) +
  facet_wrap(~Method, scales = "free_y") + expand_limits(y = 0) +
  labs(title = "起動時間と fact の実行時間 (プロセス外, DAP)", y = "ms", x = NULL) +
  theme(legend.position = "none"), "dap-time.png")

peak <- samples("results", "Dap")
peak$Version <- factor(peak$Version, versions)
peak$MB <- peak$Value / 2^20
save(ggplot(peak, aes(Version, MB, fill = Version)) +
  geom_boxplot(outlier.shape = NA) + geom_jitter(width = 0.15, size = 1) + expand_limits(y = 0) +
  labs(title = "fact のピーク Private Bytes", y = "MB", x = NULL) +
  theme(legend.position = "none"), "fact-peak.png", w = 5)

# Continue と ReverseContinue
hist <- with_n(rbind(measurements("results", "History", "current"), measurements("v0.5.0", "History", "v0.5.0")))
save(ggplot(hist, aes(N, Ms, colour = Version)) +
  geom_point(alpha = 0.4) + stat_summary(fun = median, geom = "line") +
  facet_wrap(~Method, scales = "free_y") + expand_limits(y = 0) +
  labs(title = "Continue と ReverseContinue の時間 (インプロセス)", x = "命令数 N", y = "ms"), "history-time.png", w = 9)

# 1ステップあたりの履歴メモリ
ret <- rbind(samples("results", "History", "current"), samples("v0.5.0", "History", "v0.5.0"))
ret$N <- as.integer(ret$Param)
ret$Version <- factor(ret$Version, versions)
ret$KB <- ret$Value / 1024
save(ggplot(ret, aes(N, KB, colour = Version)) +
  geom_point(alpha = 0.4) + geom_smooth(method = "lm", formula = y ~ x, se = FALSE) +
  expand_limits(y = 0) +
  labs(title = "履歴が保持するメモリ (GC.GetTotalMemory(true) の差分)", x = "命令数 N", y = "KB"), "history-retained.png")

# 表
med <- function(f, d) aggregate(f, d, median)
cat("\n## 起動時間と fact (ms, 中央値)\n")
print(med(Ms ~ Method + Version, dap), row.names = FALSE)
cat("\n## fact のピーク Private Bytes (MB, 中央値)\n")
print(med(MB ~ Version, peak), row.names = FALSE)
cat("\n## Continue と ReverseContinue (中央値)\n")
h <- med(Ms ~ Method + Version + N, hist)
h$MinstrPerSec <- h$N / h$Ms / 1e3
print(h[order(h$Method, h$Version, h$N), ], row.names = FALSE)
cat("\n## 1ステップあたりの履歴メモリ (回帰の傾き, B/step)\n")
for (v in versions) {
  cat(sprintf("%-8s %8.1f\n", v, coef(lm(Value ~ N, ret[ret$Version == v, ]))[["N"]]))
}
