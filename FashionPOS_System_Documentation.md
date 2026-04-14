# FashionPOS System Documentation

## 1. What FashionPOS Is

FashionPOS is an offline-first point-of-sale and inventory management system built for clothing and fashion businesses. It is designed for shops that need to sell products quickly at the counter, track stock accurately, manage users with different permission levels, and report on sales and profit without relying on cloud infrastructure.

The app is a desktop application built with WPF and .NET, backed by a local SQLite database. That means it can run in a shop environment even when internet access is limited or unavailable.

## 2. The Problem It Solves

FashionPOS solves the most common operational problems in fashion retail:

1. Fast sales processing at the counter.
2. Stock tracking for products with variants like size and color.
3. Safe user access with different roles.
4. Clear visibility into revenue, profit, and low-stock items.
5. Importing large product catalogs from Excel, CSV, and Shopify exports.
6. Handling pop-up bazars, fairs, and event-based sales separately from normal store sales.

For a clothing business, the hardest part is usually not just making a sale. It is keeping the product catalog clean, tracking variants correctly, avoiding overselling, and knowing what is selling well. FashionPOS is built to do those things in one place.

## 3. Who It Is For

FashionPOS is useful for:

1. Small and medium clothing stores.
2. Boutiques with many product variants.
3. Retail teams with multiple staff roles.
4. Pop-up bazars and seasonal sales events.
5. Store owners who want local control over their data.

## 4. Main Business Value

FashionPOS helps a store:

1. Sell faster at the counter.
2. Reduce stock mistakes.
3. Keep product data organized.
4. Separate event sales from normal daily operations.
5. Give owners and managers the reporting they need.
6. Limit access to sensitive business data.

## 5. High-Level Architecture

FashionPOS follows a WPF MVVM structure.

### Layers

1. **Views** - WPF windows and screens such as Login, Dashboard, POS, Inventory, Users, Reports, and Event.
2. **ViewModels** - Screen logic, commands, state, and data binding.
3. **Services** - Business rules for auth, products, sales, imports, events, invoices, and reports.
4. **Data** - SQLite connection and database initialization.
5. **Models** - Core entities such as Product, Sale, User, Category, Collection, CartItem, and Event.

### Storage

The database is stored locally at:

`%LOCALAPPDATA%\FashionPOS\fashionpos.db`

This makes the app portable and easy to deploy on a Windows machine without a separate server.

## 6. Core Modules

### 6.1 Login

The login screen authenticates staff members before they enter the system.

What it does:

1. Accepts username and password.
2. Validates empty fields.
3. Shows errors instead of crashing.
4. Routes the user into the main window after successful login.

Why it matters:

1. Protects the store from unauthorized access.
2. Allows role-based entry into the application.

### 6.2 Dashboard

The dashboard shows a fast business snapshot.

What it does:

1. Shows today's sales.
2. Shows today's profit.
3. Shows transaction count.
4. Shows low stock alerts.
5. Shows trend and summary data.

Why it matters:

1. Owners and managers can quickly see how the store is doing.
2. It gives immediate insight without opening a report.

### 6.3 POS Screen

The POS screen is the sales terminal.

What it does:

1. Loads products into a visual card grid.
2. Supports product search by name, SKU, and barcode.
3. Adds items to a cart.
4. Increments and decrements quantity.
5. Calculates totals immediately.
6. Saves payment method.
7. Prevents out-of-stock products from being sold.
8. Completes a sale and reduces stock.
9. Generates an invoice.
10. Links the sale to an active event when one is running.

Why it matters:

1. This is the main cash register experience.
2. It is optimized for speed and clarity.

### 6.4 Inventory Screen

The inventory screen manages products.

What it does:

1. Lists all active products.
2. Supports filtering and search.
3. Opens add/edit product dialogs.
4. Deletes products with confirmation.
5. Imports products from Excel, CSV, and Shopify.
6. Refreshes the grid after changes.

Why it matters:

1. Keeps product data clean and editable.
2. Helps staff maintain stock and pricing.

### 6.5 Users Screen

The users screen manages staff accounts and permissions.

What it does:

1. Adds new users.
2. Edits existing users.
3. Sets roles: Owner, Manager, Cashier.
4. Saves passwords securely.
5. Controls which screens a user can access.

Why it matters:

1. Limits access to sensitive functions.
2. Separates admin tasks from cashier tasks.

### 6.6 Event Mode

Event mode is for bazars, pop-up shops, and time-limited sales sessions.

What it does:

1. Starts and ends a session.
2. Tracks event-specific sales.
3. Shows event revenue, profit, items sold, and transaction count.
4. Links POS sales to the active event automatically.

Why it matters:

1. Event sales can be measured separately from normal store sales.
2. It is useful for temporary selling environments.

### 6.7 Reports Screen

The reports screen provides financial and transaction reporting.

What it does:

1. Loads sales by date range.
2. Summarizes revenue and profit.
3. Shows a transaction table.
4. Exports data to Excel.

Why it matters:

1. Owners can review performance over time.
2. The exported data can be used for accounting or analysis.

## 7. Data Model Summary

FashionPOS uses these major entities:

1. **User** - staff account and permission role.
2. **Product** - item for sale, including SKU, size, color, price, and stock.
3. **Category** - product classification.
4. **Collection** - seasonal or merchandising grouping.
5. **CartItem** - temporary sale line before checkout.
6. **Sale** - completed transaction.
7. **SaleItem** - sale line item inside a sale.
8. **StockMovement** - stock history ledger.
9. **BazarEvent** - event or bazar session.
10. **DashboardStats** - summary data for the dashboard.
11. **ImportResult** - import status and error details.

## 8. Key Workflows

### 8.1 Sale Workflow

1. User logs in.
2. POS screen loads products.
3. Cashier searches or clicks products.
4. Items are added to the cart.
5. Quantity is adjusted if needed.
6. Payment method is selected.
7. Sale is completed.
8. Stock is reduced.
9. Invoice is created.
10. Dashboard and event stats refresh.

### 8.2 Inventory Workflow

1. Inventory screen loads products.
2. User adds or edits a product.
3. Category and collection are selected.
4. Product is saved.
5. Product list refreshes.

### 8.3 Import Workflow

1. User imports Excel, CSV, or Shopify data.
2. The system parses the file.
3. Products are created or updated.
4. Categories are normalized.
5. The inventory grid refreshes automatically.

### 8.4 Event Workflow

1. Manager or owner starts an event.
2. POS sales are linked to the event.
3. Event statistics update in real time.
4. Event ends.
5. Session data is preserved for reporting.

## 9. Security and Permissions

The system uses role-based access control.

### Owner

1. Can access everything.
2. Can manage users.
3. Can view cost price and profit.
4. Can manage inventory.
5. Can access the dashboard and reports.

### Manager

1. Can manage inventory.
2. Can view reports.
3. Can access the dashboard.
4. Cannot manage users.
5. Cannot view profit.

### Cashier

1. Can access POS.
2. Can use event mode.
3. Cannot manage users.
4. Cannot see owner-only financial data.

## 10. What Makes It Different

FashionPOS is not just a generic POS. It is tuned for fashion retail.

It handles:

1. Size and color variants.
2. Clothing-style categories and collections.
3. Shopify import cleanup.
4. Event/bazar sales.
5. Stock movement tracking.
6. Owner/manager/cashier permissions.

## 11. Current Strengths

1. Offline-first.
2. Local SQLite database.
3. WPF desktop UI with fast desktop workflow.
4. Structured MVVM codebase.
5. Product imports from several formats.
6. Event-aware sales handling.
7. Clear role separation.
8. Dashboard and reporting support.

## 12. Feature Options You Could Add Next

If you want to expand the system, these are the best next features:

### Sales and POS

1. Barcode scanner optimization.
2. Hold cart / resume cart.
3. Partial payments.
4. Split payment between cash and card.
5. Receipt reprint from sale history.
6. Return and refund workflow.

### Inventory

1. Stock take / physical count screen.
2. Bulk edit prices.
3. Bulk stock adjustment.
4. Low-stock reorder list.
5. Supplier tracking.
6. Purchase order workflow.

### Reporting

1. Profit by product category.
2. Profit by event.
3. Best-selling sizes and colors.
4. Daily closing summary.
5. Monthly trend dashboard.
6. Export PDF reports in addition to Excel.

### Users and Security

1. Login audit history.
2. PIN-based cashier login.
3. Session timeout and auto-lock.
4. Two-factor approval for admin actions.
5. Action logs for sensitive operations.

### Customer and CRM

1. Customer profiles.
2. Customer purchase history.
3. Loyalty points.
4. SMS or email receipts.
5. VIP/customer segmentation.

### Event Mode

1. Separate event targets and goals.
2. Event vendor tracking.
3. Event settlement summary.
4. Event-specific pricing rules.

### Integrations

1. Cloud backup.
2. Multi-device sync.
3. Printer integration improvements.
4. Accounting exports.
5. Shopify re-sync and product reconciliation.

### Usability

1. Keyboard shortcuts for POS.
2. Faster search with barcode focus.
3. Saved filters in inventory and reports.
4. Better mobile-responsive layouts for touch screens.

## 13. Recommended Next Steps

If you want to evolve FashionPOS, the most valuable next additions are:

1. Customer accounts and purchase history.
2. Return/refund management.
3. Purchase orders and supplier records.
4. Better stock audit and stock count tools.
5. Cloud backup/sync for disaster recovery.

## 14. Short Summary

FashionPOS is a local desktop retail system for fashion businesses. It helps shops sell products quickly, manage stock accurately, control staff permissions, track bazar or event sales separately, and produce useful business reports. Its main value is keeping clothing retail operations organized, fast, and reliable without requiring a cloud backend.
