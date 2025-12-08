import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy import stats

df = pd.read_csv("./logs/results.csv")

groups = df.groupby(["bytes","k"])
means = groups["time"].mean()
stds = groups["time"].std()
ns = groups["time"].count()
cis = 1.96 * stds / np.sqrt(ns)  # 95% CI

# plot
byte_sizes = sorted(df["bytes"].unique())
x = np.arange(len(byte_sizes))
width = 0.35

fig, ax = plt.subplots(figsize=(10,6))

for i, k_val in enumerate([1,8]):
    vals = [means[(b,k_val)] for b in byte_sizes]
    errs = [cis[(b,k_val)] for b in byte_sizes]
    ax.bar(x + (i-0.5)*width, vals, width, yerr=errs, capsize=5, label=f"k={k_val}")

ax.set_xticks(x)
ax.set_xticklabels([str(b) for b in byte_sizes])
ax.set_xlabel("Message Size (bytes)")
ax.set_ylabel("Mean Transfer Time (s)")
ax.set_title("Mean Transfer Time ± 95% CI")
ax.legend()
plt.tight_layout()
plt.show()
