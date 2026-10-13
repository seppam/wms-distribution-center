import { describe, expect, it } from 'vitest';
import { errorMessage } from '../services/api';
import { badgeClass, fmtNum } from './format';
import { roleCan } from './permissions';

describe('roleCan (mirrors the API permission matrix)', () => {
  const matrix = {
    read: { Admin: true, Supervisor: true, Operator: true, Viewer: true, Custom: true },
    operate: { Admin: true, Supervisor: true, Operator: true, Viewer: false, Custom: false },
    manage: { Admin: true, Supervisor: true, Operator: false, Viewer: false, Custom: false },
    admin: { Admin: true, Supervisor: false, Operator: false, Viewer: false, Custom: false },
  };
  for (const [policy, roles] of Object.entries(matrix)) {
    for (const [role, expected] of Object.entries(roles)) {
      it(`${role} ${expected ? 'can' : 'cannot'} ${policy}`, () => expect(roleCan(role, policy)).toBe(expected));
    }
  }
  it('anonymous users can do nothing', () => expect(roleCan(undefined, 'read')).toBe(false));
});

describe('errorMessage', () => {
  it('prefers the API message', () => expect(errorMessage({ response: { status: 400, data: { message: 'Over-pick rejected' } } })).toBe('Over-pick rejected'));
  it('reads validation errors', () => expect(errorMessage({ response: { status: 400, data: { errors: { Qty: ['Qty must be positive'] } } } })).toBe('Qty must be positive'));
  it('explains 403 and 429', () => {
    expect(errorMessage({ response: { status: 403, data: {} } })).toMatch(/permission/);
    expect(errorMessage({ response: { status: 429, data: {} } })).toMatch(/Too many/);
  });
  it('explains an unreachable API', () => expect(errorMessage({ code: 'ERR_NETWORK' })).toMatch(/Cannot reach/));
  it('falls back', () => expect(errorMessage({}, 'Nope')).toBe('Nope'));
});

describe('format helpers', () => {
  it('formats numbers and gaps', () => {
    expect(fmtNum(1234.5)).toMatch(/1,?234\.5/);
    expect(fmtNum(null)).toBe('-');
  });
  it('maps statuses to badge classes', () => {
    expect(badgeClass('Shipped')).toBe('badge badge-success');
    expect(badgeClass('Cancelled')).toBe('badge badge-danger');
    expect(badgeClass('Unknown')).toBe('badge badge-default');
  });
});
