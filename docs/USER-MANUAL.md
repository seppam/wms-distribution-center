# User Manual

How to work in the WMS web application. All names, quantities and accounts in this manual and in the screenshots are fictional demo data.

## 1. Signing in

Open the web app (`http://localhost:5173` for local development, `http://localhost:5174` with Docker) and sign in.
In local development with the demo seed there are four accounts, one per role, all with the documented demo password
`Demo!Pass123`: `admin`, `supervisor`, `operator`, `viewer`. In any other setup the first administrator is created from the
`ADMIN_PASSWORD` setting (see the README) and creates further users under **Users**.

Failed sign-ins show "Invalid username or password". After ten attempts within a minute from one address the server
answers "Too many attempts" for a while.

## 2. Roles: what you can do

| | Viewer | Operator | Supervisor | Admin |
|---|---|---|---|---|
| Look at everything | yes | yes | yes | yes |
| Receive and put away goods, complete receipts | | yes | yes | yes |
| Confirm picks, pack, ship, mark delivered | | yes | yes | yes |
| Propose adjustments, create transfers, receive returns | | yes | yes | yes |
| Plan receipts and orders, allocate stock, assign pickers, cancel orders | | | yes | yes |
| Approve or reject adjustments, process returns | | | yes | yes |
| Edit master data (products, locations, suppliers, ...) | | | yes | yes |
| Delete master data, manage users and roles | | | | yes |

Buttons you are not allowed to use are hidden. The server enforces the same rules, so a hidden action can never be forced.

## 3. The warehouse cycle

### 3.1 Receiving and put-away (Inbound > Receipts)

1. A supervisor chooses **New receipt**: supplier, warehouse, PO number and the expected lines (product, quantity, cost, lot, expiry).
2. When the truck arrives an operator opens the receipt and chooses **Receive** on a line: received quantity, how many are
   damaged, and the **put-away location**. Good stock is placed in that bin immediately; damaged goods are recorded but not stocked.
   You can receive a line again to correct the count: only the difference is posted.
3. **Complete receipt** closes it as *Received* (everything matched) or *Partial* (something short, damaged or untouched).

### 3.2 Orders: allocate, pick, pack, ship (Outbound)

1. A supervisor chooses **New order**: warehouse, priority, destination, due date and lines. The form shows how much stock
   is available for each product.
2. Open the order and choose **Allocate stock and start picking**. Stock is reserved first-expiry-first-out and one pick task per
   bin is created. If any line cannot be fully covered, nothing is reserved and the message names the product and the shortfall.
3. On **Pick Tasks** an operator enters the quantity found and chooses **Confirm**. Picking more than requested is rejected;
   picking less records a *short pick* and releases the missing reservation. The order becomes *Picked* when every task is done.
4. Open the order again: **Pack** (carton number and weight), then **Ship** (carrier, tracking, vehicle, driver). Shipping
   deducts the stock and writes the movement to the ledger. **Shipments > Mark delivered** completes the order.
5. A supervisor can **Cancel order** at any point before shipping; reserved stock is released.

### 3.3 Adjustments (Adjustments)

An operator proposes a correction: *decrease*, *increase* or *recount* (set the counted quantity). Nothing changes until a
supervisor chooses **Approve**; the quantity is then recalculated against the current stock and a ledger entry is written.
Stock cannot go below zero or below what is reserved for orders. Supervisors cannot approve their own proposals unless they are Admin.

### 3.4 Transfers (Transfers)

Choose the stock to move (only unreserved stock is listed), the destination bin and the quantity, then **Complete** the
transfer to move it. Lots are never mixed in one bin. A draft transfer can be cancelled.

### 3.5 Returns (Returns)

An operator chooses **Receive return** (optionally against a shipped order; the quantity is checked against what shipped).
A supervisor then chooses **Process**: *Restock* puts good items back into a bin, *Refurbish*, *Dispose* and *Write off*
only record the decision. Items that are not in good condition cannot be restocked.

### 3.6 Inventory and the ledger (Inventory)

**Stock** shows on-hand, reserved and available quantity per bin. **Movement ledger** lists every inbound, outbound,
adjustment, transfer and return posting with the quantity before and after; entries are never edited or deleted.

## 4. Master data

Products, categories, units of measure, suppliers, carriers, warehouses, zones and locations are maintained on their own
pages (supervisors edit, admins delete). A product needs a unit of measure; locations belong to a zone, zones to a warehouse.
Deleting a record that is still referenced is refused with an explanation.

## 5. Users and roles (Admin)

Admins create users with a role (password of at least 10 characters), deactivate them, or delete them (not themselves).
A role change or deactivation takes effect on the next request. The four built-in roles cannot be renamed or deleted;
custom roles only get read access.

## 6. Troubleshooting

| Symptom | Cause |
|---|---|
| "You do not have permission to do that." | Your role does not allow the action (see section 2) |
| "Insufficient available stock for SKU-..." | Other orders have reserved the stock, or it was never received |
| "Stock was allocated by another order at the same time" | Two people allocated the same stock simultaneously; retry |
| "Cannot reach the server" | The API is not running or the proxy target is wrong |
| Receipt line cannot be received | The receipt is already Received/Partial, or the location belongs to another warehouse or holds another lot |
