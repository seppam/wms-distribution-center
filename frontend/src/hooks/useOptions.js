import { useLoad } from './useLoad';

/** Loads a list endpoint and maps it to <Select> options. */
export function useOptions(fetcher, toOption) {
  const { data, error, loading, reload } = useLoad(fetcher, [], { initial: [] });
  const rows = Array.isArray(data) ? data : [];
  return { options: rows.map((r) => ({ value: r.id, label: toOption(r) })), rows, error, loading, reload };
}

export const enumOptions = (values) => values.map((v) => ({ value: v, label: v }));
