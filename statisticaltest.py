import pandas as pd
import numpy as np
from scipy import stats

df = pd.read_csv('./logs/results.csv')

# calculate mean times for each (k, bytes) combination
mean_times = df.groupby(['k', 'bytes'])['time'].mean().reset_index()

pivot = mean_times.pivot(index='bytes', columns='k', values='time')
pivot.columns = ['k1', 'k8']

print("Mean Transfer Times:")
print(pivot)
print("\n" + "="*60)

# percentage improvement
pivot['improvement_%'] = (pivot['k1'] - pivot['k8']) / pivot['k1'] * 100
pivot['time_saved_s'] = pivot['k1'] - pivot['k8']

print("\nPerformance Improvements:")
print(pivot[['improvement_%', 'time_saved_s']])
print("\n" + "="*60)

# effect size (Cohen's d)
differences = pivot['k1'] - pivot['k8']
cohens_d = np.mean(differences) / np.std(differences, ddof=1)
print(f"  Cohen's d: {cohens_d:.4f}")

# individual two-sample t test per message size
print("\nPer-Message-Size Statistical Tests:")
for msg_size in df['bytes'].unique():
    k1_data = df[(df['k'] == 1) & (df['bytes'] == msg_size)]['time']
    k8_data = df[(df['k'] == 8) & (df['bytes'] == msg_size)]['time']

    t_stat, p_val = stats.ttest_ind(k1_data, k8_data)
    mean_improvement = (k1_data.mean() - k8_data.mean()) / k1_data.mean() * 100

    print(f"\n  {msg_size} bytes:")
    print(f"    k=1 mean: {k1_data.mean():.3f}s (n={len(k1_data)})")
    print(f"    k=8 mean: {k8_data.mean():.3f}s (n={len(k8_data)})")
    print(f"    Improvement: {mean_improvement:.2f}%")
    print(f"    t-test: t={t_stat:.3f}, p={p_val:.6f}")
