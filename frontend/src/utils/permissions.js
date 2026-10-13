// Mirrors the API authorization policies (see the permission matrix in the README).
// The API is the source of truth; this only hides actions the server would reject.
export const POLICY_ROLES = {
  read: ['Admin', 'Supervisor', 'Operator', 'Viewer'],
  operate: ['Admin', 'Supervisor', 'Operator'],
  manage: ['Admin', 'Supervisor'],
  admin: ['Admin'],
};

export function roleCan(role, policy) {
  if (policy === 'read') return !!role; // any signed-in user, including custom roles
  return (POLICY_ROLES[policy] || []).includes(role);
}
