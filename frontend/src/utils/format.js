export const fmtDate = (d) => (d ? new Date(d).toLocaleDateString() : '-');
export const fmtDateTime = (d) => (d ? new Date(d).toLocaleString() : '-');
export const fmtNum = (n) => (n == null ? '-' : Number(n).toLocaleString(undefined, { maximumFractionDigits: 2 }));

const BADGES = {
  // inbound
  Draft: 'default', InProgress: 'warning', Received: 'success', Partial: 'info', Cancelled: 'danger',
  Pending: 'default', Damaged: 'danger', Shortage: 'warning',
  // outbound
  Picking: 'info', Picked: 'warning', Packed: 'warning', Shipped: 'success', Completed: 'success',
  // pick tasks / shipments
  Assigned: 'info', Dispatched: 'info', InTransit: 'info', Delivered: 'success',
  // adjustments, returns, transfers
  Approved: 'success', Applied: 'success', Inspecting: 'warning', Processed: 'success',
  // priority
  Normal: 'default', Urgent: 'warning', Rush: 'danger',
  // inventory
  Available: 'success', Hold: 'warning', Reserved: 'info', Expired: 'danger',
};

export const badgeClass = (status) => `badge badge-${BADGES[status] || 'default'}`;
