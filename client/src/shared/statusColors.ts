export const statusTextColors = {
  accent: 'text-accent',
  danger: 'text-error',
  dangerStrong: 'text-error',
  neutral: 'text-slate-700',
  success: 'text-success',
  warning: 'text-warning',
} as const;

export const statusBorderColors = {
  dangerFocus: 'border-error focus:border-error',
} as const;

export const statusSurfaceColors = {
  danger: 'border border-error/20 bg-error/10 text-error',
  dangerCard: 'border border-main-border bg-error/10',
} as const;

export const userStatusBadgeColors = {
  excluded: 'bg-slate-200 text-slate-700',
  included: 'bg-success/15 text-success',
} as const;

export const freshnessStatusBadgeColors = {
  deferred: 'bg-slate-100 text-slate-600',
  planned: 'bg-warning/10 text-warning',
  ready: 'bg-success/10 text-success',
} as const;

export const issueToneDotColors = {
  error: 'bg-error',
  neutral: 'bg-slate-400',
  warning: 'bg-warning',
} as const;

export const breakdownToneColors = [
  {
    bar: 'bg-primary',
    dot: 'bg-primary',
  },
  {
    bar: 'bg-success',
    dot: 'bg-success',
  },
  {
    bar: 'bg-warning',
    dot: 'bg-warning',
  },
  {
    bar: 'bg-accent',
    dot: 'bg-accent',
  },
] as const;
