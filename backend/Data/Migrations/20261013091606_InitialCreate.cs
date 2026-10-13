using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WmsDistributionCenter.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carriers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    contact_person = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    vehicle_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carriers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    permissions = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    contact_person = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "uoms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: true),
                    conversion_factor = table.Column<decimal>(type: "numeric", nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uoms", x => x.id);
                    table.ForeignKey(
                        name: "FK_uoms_uoms_base_uom_id",
                        column: x => x.base_uom_id,
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    capacity = table.Column<decimal>(type: "numeric", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    volume_m3 = table.Column<decimal>(type: "numeric", nullable: false),
                    barcode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    min_stock = table.Column<int>(type: "integer", nullable: false),
                    max_stock = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_products_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    zone_type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_zones_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inbound_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    asn_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expected_date = table.Column<DateOnly>(type: "date", nullable: true),
                    received_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    dock_door = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_inbound_receipts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inbound_receipts_users_received_by",
                        column: x => x.received_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inbound_receipts_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbound_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    destination = table.Column<string>(type: "text", nullable: true),
                    requested_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbound_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbound_orders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outbound_orders_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    aisle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rack = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    location_type = table.Column<int>(type: "integer", nullable: false),
                    max_capacity = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_locations_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordered_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    picked_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    shipped_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_outbound_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "outbound_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pack_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carton_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    packed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    packed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pack_lists", x => x.id);
                    table.ForeignKey(
                        name: "FK_pack_lists_outbound_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "outbound_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pack_lists_users_packed_by",
                        column: x => x.packed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    condition = table.Column<int>(type: "integer", nullable: false),
                    disposition = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_orders_outbound_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "outbound_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_return_orders_users_received_by",
                        column: x => x.received_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_return_orders_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bol_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vehicle_plate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    driver_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    shipped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipments_carriers_carrier_id",
                        column: x => x.carrier_id,
                        principalTable: "carriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_outbound_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "outbound_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    adj_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adj_type = table.Column<int>(type: "integer", nullable: false),
                    qty_before = table.Column<decimal>(type: "numeric", nullable: false),
                    adj_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    qty_after = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_adjustments_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adjustments_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adjustments_users_approved_by",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_adjustments_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty_on_hand = table.Column<decimal>(type: "numeric", nullable: false),
                    qty_reserved = table.Column<decimal>(type: "numeric", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    last_counted_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventories", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventories_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventories_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_type = table.Column<int>(type: "integer", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<int>(type: "integer", nullable: false),
                    qty_before = table.Column<decimal>(type: "numeric", nullable: false),
                    qty_after = table.Column<decimal>(type: "numeric", nullable: false),
                    uom_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    received_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    damaged_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    putaway_location_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_receipt_items_inbound_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "inbound_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_receipt_items_locations_putaway_location_id",
                        column: x => x.putaway_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_receipt_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    transferred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_transfers_locations_from_location_id",
                        column: x => x.from_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_locations_to_location_id",
                        column: x => x.to_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pick_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty_to_pick = table.Column<decimal>(type: "numeric", nullable: false),
                    qty_picked = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    picker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    picked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pick_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_pick_tasks_locations_from_location_id",
                        column: x => x.from_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_tasks_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pick_tasks_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_tasks_users_picker_id",
                        column: x => x.picker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_adj_number",
                table: "adjustments",
                column: "adj_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_approved_by",
                table: "adjustments",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_created_by",
                table: "adjustments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_location_id",
                table: "adjustments",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_product_id",
                table: "adjustments",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_receipts_receipt_no",
                table: "inbound_receipts",
                column: "receipt_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_receipts_received_by",
                table: "inbound_receipts",
                column: "received_by");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_receipts_supplier_id",
                table: "inbound_receipts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_receipts_warehouse_id",
                table: "inbound_receipts",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventories_location_id",
                table: "inventories",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventories_product_id_location_id",
                table: "inventories",
                columns: new[] { "product_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_location_id",
                table: "inventory_transactions",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_product_id",
                table: "inventory_transactions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_user_id",
                table: "inventory_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_locations_location_code",
                table: "locations",
                column: "location_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_zone_id",
                table: "locations",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_created_by",
                table: "outbound_orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_order_number",
                table: "outbound_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_warehouse_id",
                table: "outbound_orders",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_pack_lists_order_id",
                table: "pack_lists",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_pack_lists_packed_by",
                table: "pack_lists",
                column: "packed_by");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_from_location_id",
                table: "pick_tasks",
                column: "from_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_order_item_id",
                table: "pick_tasks",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_picker_id",
                table: "pick_tasks",
                column: "picker_id");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_product_id",
                table: "pick_tasks",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sku",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_uom_id",
                table: "products",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_product_id",
                table: "receipt_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_putaway_location_id",
                table: "receipt_items",
                column: "putaway_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_receipt_id",
                table: "receipt_items",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_order_id",
                table: "return_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_received_by",
                table: "return_orders",
                column: "received_by");

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_return_no",
                table: "return_orders",
                column: "return_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_warehouse_id",
                table: "return_orders",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_carrier_id",
                table: "shipments",
                column: "carrier_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_order_id",
                table: "shipments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_code",
                table: "suppliers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfers_from_location_id",
                table: "transfers",
                column: "from_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_product_id",
                table: "transfers",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_requested_by",
                table: "transfers",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_to_location_id",
                table: "transfers",
                column: "to_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_transfer_no",
                table: "transfers",
                column: "transfer_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_uoms_base_uom_id",
                table: "uoms",
                column: "base_uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_uoms_code",
                table: "uoms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_warehouse_id",
                table: "users",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zones_warehouse_id",
                table: "zones",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjustments");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "inventories");

            migrationBuilder.DropTable(
                name: "inventory_transactions");

            migrationBuilder.DropTable(
                name: "pack_lists");

            migrationBuilder.DropTable(
                name: "pick_tasks");

            migrationBuilder.DropTable(
                name: "receipt_items");

            migrationBuilder.DropTable(
                name: "return_orders");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "transfers");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "inbound_receipts");

            migrationBuilder.DropTable(
                name: "carriers");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "outbound_orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "zones");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "uoms");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "warehouses");
        }
    }
}
