/** Enum member names exactly as the API serialises them (JsonStringEnumConverter). */
export const ZoneType = ['Receiving', 'Storage', 'Packing', 'Shipping', 'Returns', 'Staging'] as const;
export const LocationType = ['Pick', 'Floor', 'Bulk', 'Reserved', 'Damage'] as const;
export const LocationStatus = ['Empty', 'Occupied', 'Partial', 'Locked'] as const;
export const OrderType = ['Sales', 'Transfer', 'Internal', 'Sample'] as const;
export const Priority = ['Normal', 'Urgent', 'Rush'] as const;
export const AdjType = ['Positive', 'Negative', 'Recount'] as const;
export const ReturnCondition = ['Good', 'Damaged', 'Expired', 'WrongItem'] as const;
export const Disposition = ['Restock', 'Refurbish', 'Dispose', 'WriteOff'] as const;
export const InventoryStatus = ['Available', 'Reserved', 'Hold', 'Damaged', 'Expired'] as const;
export const PickTaskStatus = ['Assigned', 'InProgress', 'Picked', 'Cancelled'] as const;

export const Roles = { Admin: 'Admin', Supervisor: 'Supervisor', Operator: 'Operator', Viewer: 'Viewer' } as const;
export const FLOOR_ROLES: string[] = [Roles.Operator, Roles.Supervisor, Roles.Admin];

/** The demo accounts (username is the lower-case role name), same as DemoDataSeeder.Accounts. */
export const DEMO_ACCOUNTS = [
  { role: 'Admin', username: 'admin' },
  { role: 'Supervisor', username: 'supervisor' },
  { role: 'Operator', username: 'operator' },
  { role: 'Viewer', username: 'viewer' },
] as const;
