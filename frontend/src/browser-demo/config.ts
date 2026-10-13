/** The shared, public demo password of the four demo accounts (same as the Docker demo; fictional data only). */
export const DEMO_PASSWORD: string = (import.meta.env?.VITE_DEMO_PASSWORD as string | undefined) || 'Demo!Pass123';
