import seedFile from '../../../contracts/demo-seed.json';
import { DEMO_PASSWORD } from './config';
import { TABLES, type Db, type SeedFile } from './types';
import { addDays, compactDay, dayString, hashPassword, sha256Hex } from './util';

/**
 * Expands the shared dataset contract (contracts/demo-seed.json, generated from the backend's DemoDataSeeder) into a database:
 *   "@seed"   -> the moment the demo data is created (rows keep their creation order, one millisecond apart)
 *   "@d:+N"   -> a date N days from today
 *   "{today}" -> yyyyMMdd (document numbers such as ORD-{today}-0001)
 */
export const SEED: SeedFile = seedFile as unknown as SeedFile;

/** Short fingerprint of the dataset: saved data from a different dataset is discarded instead of being mixed with it. */
export const seedRevision = (seed: SeedFile = SEED): string => sha256Hex(JSON.stringify(seed.tables)).slice(0, 16);

export function buildSeed(now: Date, seed: SeedFile = SEED, password: string = DEMO_PASSWORD): Db {
  const today = compactDay(now);
  const db = {} as Record<string, Record<string, unknown>[]>;
  for (const table of TABLES) {
    const rows = seed.tables[table] ?? [];
    db[table] = rows.map((row, index) => {
      const stamp = new Date(now.getTime() - (rows.length - 1 - index)).toISOString();
      const out: Record<string, unknown> = {};
      for (const [key, value] of Object.entries(row)) {
        if (value === '@seed') out[key] = stamp;
        else if (typeof value === 'string' && /^@d:[+-]?\d+$/.test(value)) out[key] = dayString(addDays(now, Number(value.slice(3))));
        else if (typeof value === 'string' && value.includes('{today}')) out[key] = value.replaceAll('{today}', today);
        else out[key] = value;
      }
      return out;
    });
  }
  // The dataset carries no credentials: the shared public demo password is applied to the four demo accounts here.
  const hash = hashPassword(password);
  for (const user of db.users) user.passwordHash = hash;
  return db as unknown as Db;
}
