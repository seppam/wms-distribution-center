import { DEMO_ACCOUNTS } from '../enums';
import { byId } from '../db';
import { userInfo } from '../dto';
import { NotFoundError, UnauthorizedError } from '../errors';
import { ok, route, type Route } from '../route';
import { asBody, hashPassword, Validator } from '../util';

const TOKEN_PREFIX = 'wms-browser-demo';
export const SESSION_MINUTES = 480;

export const makeToken = (userId: string, now: Date): string => `${TOKEN_PREFIX}.${userId}.${now.getTime() + SESSION_MINUTES * 60_000}`;

/** Returns the user id of a still valid token, or null. (The real API validates a signed JWT; here there is nothing to forge: everything is local.) */
export function parseToken(token: string | null | undefined, now: Date): string | null {
  if (!token) return null;
  const parts = token.split('.');
  if (parts.length !== 3 || parts[0] !== TOKEN_PREFIX) return null;
  return Number(parts[2]) > now.getTime() ? parts[1] : null;
}

export const authRoutes: Route[] = [
  route('POST', 'api/auth/login', (c) => {
    const body = asBody(c.body);
    new Validator(body).required('username', 'Username').required('password', 'Password').done();
    const user = c.db.users.find((u) => u.username === body.username && u.isActive);
    // Same answer for an unknown user and a wrong password.
    if (!user || user.passwordHash !== hashPassword(String(body.password))) throw new UnauthorizedError('Invalid username or password');
    user.lastLogin = c.now.toISOString();
    return ok({ token: makeToken(user.id, c.now), expiresAt: new Date(c.now.getTime() + SESSION_MINUTES * 60_000).toISOString(), user: userInfo(c.db, user) });
  }),
  route('POST', 'api/auth/register', () => {
    // Self-registration is off by default in the real API (Auth__AllowSelfRegistration) and there is nobody to register with here.
    throw new NotFoundError('Self-registration is disabled. Ask an administrator for an account.');
  }),
  route('GET', 'api/auth/me', (c) => {
    const user = byId(c.db.users, c.user?.id);
    if (!user) throw new NotFoundError();
    return ok(userInfo(c.db, user));
  }),
  route('GET', 'api/demo-info', () => ok({ enabled: true, accounts: DEMO_ACCOUNTS.map((a) => ({ role: a.role, username: a.username })) })),
  route('POST', 'api/demo/reset', (c) => {
    c.resetDataset();
    return ok({ message: 'Demo data restored.' });
  }),
];
