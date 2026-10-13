import { useCallback, useEffect, useRef, useState } from 'react';
import { errorMessage } from '../services/api';

/**
 * Loads data with explicit loading / error states (no silent fallbacks).
 * `fetcher` returns an axios promise; `select` maps the response (defaults to res.data).
 */
export function useLoad(fetcher, deps = [], { select = (res) => res.data, initial = null } = {}) {
  const [data, setData] = useState(initial);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const fetcherRef = useRef(fetcher);
  useEffect(() => { fetcherRef.current = fetcher; });

  const reload = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const res = await fetcherRef.current();
      setData(select(res));
    } catch (err) {
      setError(errorMessage(err, 'Failed to load data'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, [reload, ...deps]);

  return { data, loading, error, reload, setData };
}
