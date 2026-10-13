import { useEffect, useState } from 'react';
import { loadDemoInfo } from '../services/demo';
import { browserDemoInfo } from '#browser-demo';

/** { enabled, accounts: [{ role, username }] }; enabled stays false until the API confirms demo mode. */
export default function useDemoInfo() {
  const [info, setInfo] = useState(browserDemoInfo ?? { enabled: false, accounts: [] });
  useEffect(() => {
    let live = true;
    loadDemoInfo().then((i) => { if (live) setInfo({ enabled: Boolean(i?.enabled), accounts: i?.accounts ?? [] }); });
    return () => { live = false; };
  }, []);
  return info;
}
