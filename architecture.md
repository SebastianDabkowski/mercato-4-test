# Mercato — architecture.md

## Architecture Overview
Mercato is a multi-vendor marketplace that connects sellers and buyers in a single web platform. The MVP focuses on end-to-end buying flow (catalog → cart → checkout → payment → seller fulfilment) with marketplace escrow payments, commission calculation, basic notifications, and admin oversight.

This document describes the target architecture for MVP and the foundations for later phases (integrations, mobile/PWA enhancements, shipping providers, advanced analytics, and messaging).

## 1. Goals and Non-Goals

### 1.1 Primary Goals
1. Deliver a complete marketplace MVP in ~5–6 months with predictable delivery.
2. Support multiple sellers with isolation at the data and permission level.
3. Implement secure authentication/authorization and GDPR-aligned processing.
4. Support escrow payments, automatic commission calculation, and seller payouts.
5. Provide a scalable baseline with clear module boundaries to enable later extraction to services if needed.
6. Provide operational visibility (logging, metrics, tracing) and auditability for critical actions.

### 1.2 Non-Goals (MVP)
1. Full native mobile apps (iOS/Android) — MVP is responsive web; PWA is optional.
2. Full courier integrations (label generation, automated status tracking) — manual tracking number entry is acceptable.
3. Advanced personalization and recommendation engine.
4. Full seller multi-user RBAC matrix (beyond minimal roles) — can be phase 1.5/2.
5. Full external platform sync (Shopify/WooCommerce/Baselinker) — CSV import first; API and connectors later.

## 2. Context

### 2.1 Business Context
Mercato earns primarily from commissions on transactions. The platform is a single unified marketplace (no per-seller subdomains), with seller profile pages under the main domain and consistent UX across stores.

Key stakeholders:
Product/Founder, Buyers, Sellers, Mercato Admin/Operations, Finance/Accounting, Security/Compliance, Customer Support.

### 2.2 System Context (Text)
1. Buyer uses the Web App to browse products, checkout, pay, and track orders.
2. Seller uses the Seller Portal (same web app, role-based UI) to manage products and orders, provide tracking numbers, and review settlements.
3. Admin uses the Admin Portal to manage users/sellers, moderate content, configure fees, and view platform KPIs.
4. Payment Provider handles buyer payments and supports marketplace escrow / split/transfer logic and payouts.
5. Email Service sends verification, order, shipping, and payout notifications.
6. Object Storage stores product images and attachments.
7. Search Engine provides fast product search and filtering (optional for MVP; can start with DB search).
8. Observability Stack captures logs, metrics, traces, alerts.

## 3. Requirements

### 3.1 Functional Requirements (MVP)
Identity & Access
1. Buyer/Seller registration and login (email/password).
2. Email verification.
3. Social login for buyers (Google/Facebook), Apple later.
4. Password reset and session management with secure tokens.
5. Roles: buyer, seller, admin.

Seller Management
1. Seller onboarding wizard and store profile.
2. Seller verification (company data / tax ID; personal ID for individuals).
3. Public store page.
4. Bank account and payout preferences.

Product Catalog
1. Product CRUD with images, price, stock, category, shipping parameters.
2. Product workflow states: draft, active, suspended, archived.
3. Bulk updates (price, stock).
4. CSV/XLS import (early) and export (reporting).

Search & Navigation
1. Category pages and category tree.
2. Keyword search, filters (category, price, condition, seller), sorting and pagination.

Cart & Checkout
1. Multi-seller cart and split into seller sub-orders.
2. Stock/price validation at checkout.
3. Address management, shipping selection, payment selection.
4. Order confirmation screen + email.

Orders & Fulfilment
1. Order lifecycle: new, paid, preparing, shipped, delivered, cancelled, refunded.
2. Buyer order list and order details.
3. Seller order list and order details + export.
4. Seller updates fulfilment status and enters tracking number.

Payments & Settlements
1. Marketplace escrow model: buyer → Mercato → seller.
2. Payment statuses: pending, paid, failed, refunded.
3. Automatic commission calculation per transaction.
4. Seller payout schedule and payout history.
5. Monthly settlement summaries and commission invoices.

Returns & Disputes (Basic)
1. Buyer initiates return/complaint.
2. Seller review workflow and status tracking.
3. Link to refund processing.

Notifications
1. Email notifications for registration, orders, shipping updates, payouts.
2. Notification center in web app (basic list).

Administration
1. Admin user management: view, block, reactivate.
2. Moderation of products/photos/reviews (reviews may be phase 1.5).
3. Platform settings: fees/commission, VAT, currencies.
4. Reporting dashboard: GMV, orders, active sellers, products, new users.

### 3.2 Non-Functional Requirements
1. Security: OWASP baseline, secure headers, CSRF protection, rate limiting, secret management.
2. Privacy/GDPR: data minimization, consent management, right of access/export, deletion with anonymization.
3. Availability: target 99.5%+ for MVP (tune later), graceful degradation for external dependencies.
4. Performance: p95 page/API response time targets per critical flow (search, product page, checkout).
5. Scalability: horizontal scaling of web tier; separate scaling of background jobs.
6. Maintainability: clear module boundaries, small public interfaces, enforced dependencies.
7. Observability: structured logs, traces, metrics, error reporting, audit logs.
8. Compliance: encryption in transit and at rest; processing registry; incident logging.

## 4. Architecture Style and Principles

### 4.1 Chosen Style (MVP): Modular Monolith
Mercato will be delivered as a single deployable application (one runtime and one release unit), but internally structured as business modules (bounded contexts) with strict boundaries.

Core characteristics:
1. Each module owns its domain model and its persistence schema (table ownership and migrations).
2. Cross-module usage happens only through explicit public contracts (module interfaces) and domain events.
3. Modules do not share domain entities; they exchange identifiers and contract DTOs/events.
4. External systems (payments, email, storage, search) are integrated via adapters owned by the relevant module.

Rationale:
1. Faster MVP delivery and simpler operations than distributed services.
2. Easier consistency for transactional workflows (checkout, payments, refunds).
3. Keeps an option to extract modules into services later, with minimal refactoring.

### 4.2 Key Principles
1. Domain-first module boundaries (bounded contexts).
2. Explicit public contracts between modules (interfaces/events) and no shared database tables across modules.
3. Prefer synchronous calls within monolith only through module interfaces; use domain events for cross-module workflows.
4. Outbox pattern for integration events and reliable emails/notifications.
5. Idempotency for all payment/webhook-related handlers.
6. Defense in depth for security and least privilege for data access.

## 5. Bounded Context Map (Modules)

### 5.1 Recommended Bounded Contexts
1. Identity & Access
   Owns: authentication, sessions/tokens, password reset, social login entry points, security events, role assignments.
   Publishes events: UserRegistered, EmailVerified, UserBlocked.

2. Seller Management
   Owns: seller onboarding, store profile, verification status, payout preferences.
   Publishes events: SellerOnboarded, SellerVerified, PayoutProfileUpdated.

3. Product
   Owns: product lifecycle (draft/active/suspended/archived), product data (title, description, images, price, stock, condition), import/export, bulk updates.
   Publishes events: ProductPublished, ProductPriceChanged, ProductStockChanged, ProductSuspended.

4. Catalog & Taxonomy
   Owns: category tree, product attributes, allowed values, admin-managed reference data for classification.
   Publishes events: CategoryUpdated, AttributeUpdated.

5. Search & Discovery
   Owns: search projections/index, filtering/sorting configuration, query analytics (optional).
   Consumes events: ProductPublished/ProductUpdated, CategoryUpdated/AttributeUpdated.

6. Cart
   Owns: multi-seller cart, cart item operations, cart merge (guest→user), cart-level validations.
   Publishes events: CartCheckedOutIntent.

7. Checkout
   Owns: checkout session, address selection, shipping selection, final validation, reservations/locks (time-bound).
   Publishes events: CheckoutSubmitted, CheckoutFailedValidation.

8. Orders
   Owns: order aggregate, splitting into seller sub-orders, order status transitions and views for buyer/seller.
   Consumes events: PaymentConfirmed, PaymentFailed, RefundCompleted.
   Publishes events: OrderCreated, OrderPaid, OrderShipped, OrderDelivered, OrderCancelled.

9. Payments
   Owns: payment intent/transaction state machine, webhook ingestion, idempotency, refunds, reconciliation exports.
   Publishes events: PaymentConfirmed, PaymentFailed, RefundRequested, RefundCompleted.

10. Settlements & Commission
    Owns: commission rules, commission calculation records, payout batches/schedules, payout history, monthly settlement summaries, commission invoices.
    Consumes events: OrderPaid/Delivered, RefundCompleted.
    Publishes events: PayoutScheduled, PayoutCompleted, SettlementClosed.

11. Shipping
    Owns: seller shipping methods (cost, delivery time), tracking numbers, shipment status timeline (manual in MVP).
    Publishes events: TrackingNumberAdded, ShipmentStatusChanged.

12. Returns & Disputes
    Owns: return/complaint cases, workflow statuses, evidence metadata, links to refunds.
    Publishes events: ReturnRequested, DisputeOpened, RefundApproved.

13. Notifications
    Owns: email sending adapter, notification center records, templates, delivery logs, outbox processing.
    Consumes: domain events from other modules.

14. Reporting & Analytics
    Owns: read models and aggregated projections, dashboards, CSV exports, KPI definitions.
    Consumes: events from Orders, Payments, Settlements, Products.

15. Administration & Configuration
    Owns: platform settings (commission rates, fees, VAT, currencies), legal content management, feature flags, admin actions audit hooks.
    Publishes events: CommissionRateChanged, CurrencyRateUpdated, FeatureFlagChanged.

### 5.2 Boundary Rules (Non-negotiable)
1. Each module owns its schema (table ownership). No cross-module writes to foreign tables.
2. No shared domain entities across modules. Exchange only identifiers and contract DTOs/events.
3. Cross-module calls are allowed only via module interfaces; workflows prefer domain events and process managers.
4. External integrations are behind module-owned adapters.
5. Payment and webhook handlers must be idempotent and auditable.
6. Use an Outbox pattern for reliable notifications and integration events.

## 6. Data and Storage

### 6.1 Data Stores
1. Relational DB (primary): users, stores, products, cart, orders, settlements.
2. Object storage: product images, seller documents (verification), exports.
3. Search index (optional MVP): product search documents.
4. Cache (optional): sessions, hot reads (categories, product listings).

### 6.2 Data Ownership
Each module owns its tables and migrations. Cross-module reads should use:
1. Query models/materialized views (read-optimized projections), or
2. Controlled read APIs, or
3. Replicated data via domain events (event-driven projections).

## 7. Integration Architecture

### 7.1 Payment Provider Integration (Critical)
1. Checkout creates PaymentIntent/Transaction with idempotency key.
2. Payment provider sends webhooks for status changes.
3. Payments module validates signature, updates state machine, emits domain events.
4. Orders module reacts to “PaymentConfirmed” to move order to “paid” and create seller sub-orders.
5. Settlements module calculates commission and schedules payouts.
6. Refunds follow similar event-driven flow with audit logging.

Required properties:
1. Webhook signature verification.
2. Idempotent webhook handling.
3. Strong audit trail and reconciliation exports.
4. Retry strategy + dead-letter handling for failed callbacks.

### 7.2 Email / Notification Integration
1. Use provider (SendGrid/AWS SES/Azure Communication Services) behind an adapter.
2. Outbox + background worker to ensure reliable delivery.
3. Template versioning and localization strategy (future).

### 7.3 Storage Integration
1. Signed upload URLs for images.
2. Virus scanning pipeline for uploads (phase 2 if needed).
3. Image optimization pipeline (resize/compress) for product images.

### 7.4 Shipping (MVP and Future)
MVP: manual carrier selection + seller enters tracking number.
Future: broker/courier APIs, label generation, tracking sync.

### 7.5 External E-commerce Integrations (Future)
1. CSV/XLS import/export (early).
2. Public/private API for products/orders/stock (phase 2).
3. Connectors (Baselinker/Shopify/WooCommerce) after API stabilizes.

## 8. Security, Privacy and Compliance

### 8.1 Identity
1. Secure password hashing, account lockout, email verification.
2. MFA readiness (phase 2) — design for it now.
3. Social login via OAuth for buyers.

### 8.2 Authorization
1. Role-based access control: buyer/seller/admin.
2. Seller-scoped authorization: seller can access only their store/products/orders.
3. Admin has platform-wide access; actions audited.

### 8.3 GDPR & Data Handling
1. Processing registry and data classification.
2. Consent management for marketing communications.
3. Data export (Right of Access) for users.
4. Account deletion with anonymization (retain order/legal records as required).
5. Encryption at rest and in transit.
6. PII minimization in logs; redaction policies.

### 8.4 Audit Logging
Audit critical actions:
1. Login/security events.
2. Payment status changes, payouts, refunds.
3. Commission rate changes, VAT/currency changes.
4. Admin moderation actions.
5. User data export / deletion requests.

## 9. Observability and Operations
1. Structured logging with correlation IDs per request.
2. Distributed tracing across web + background jobs.
3. Metrics: request latency, error rate, throughput, queue depth, payment webhook success rate.
4. Alerts: payment failures spike, payout job failures, high error rates, DB saturation.
5. Dashboards: GMV, orders, sellers, payouts, refunds, disputes.

## 10. Deployment and Environments

### 10.1 Environments
1. Local: docker compose for DB and dependencies.
2. Dev: shared environment for feature testing.
3. Staging: production-like for end-to-end and load tests.
4. Production: HA setup, backups, and disaster recovery.

### 10.2 Release Strategy
1. CI pipeline: build, tests, security scan, migration check.
2. CD pipeline: deploy with migrations, smoke tests.
3. Rollback: versioned deployments and backward-compatible migrations.

## 11. Key Workflows (Text Sequence)

### 11.1 Checkout → Payment → Order Split
1. Buyer submits checkout.
2. Cart validates stock/price and locks reservation (time-bound).
3. Payments creates payment intent and returns redirect/confirmation.
4. Webhook confirms payment.
5. Orders marks order as paid and creates seller sub-orders.
6. Notifications sends confirmation to buyer and order notification to each seller.

### 11.2 Payout and Monthly Settlement
1. Settlement job aggregates eligible orders.
2. Commission calculated per transaction and stored.
3. Payout created and executed via payment provider/bank transfer integration.
4. Seller sees payout history; invoice generated for commission.

### 11.3 Return / Refund (Basic)
1. Buyer opens return request.
2. Seller reviews and decides resolution.
3. If refund: Payments processes refund; Orders updates statuses; Notifications informs buyer/seller.

## 12. Risks and Mitigations
1. Payment complexity and legal constraints (escrow): choose provider with marketplace support; early POC; strong audit.
2. Data isolation for sellers: enforce authorization at query level; automated tests; security review.
3. Performance of search/catalog: introduce search engine and caching when needed; measure early.
4. Fraud/abuse (accounts, orders): rate limiting, monitoring, dispute tools; phase 2 antifraud.
5. GDPR deletion vs accounting retention: define retention policy with legal; implement anonymization patterns.

## 13. Decisions (Link to ADRs)
Create ADRs for:
1. Architecture style (modular monolith).
2. Payment provider selection and escrow model.
3. Search technology choice.
4. Data ownership and cross-module communication approach.
5. Payout scheduling and settlement model.
6. Observability stack and logging policy.
7. Product image storage approach (ADR-005).

## 14. Open Questions — Resolved

1. Technology stack  
   ASP.NET Core with C# for backend. Web application architecture (no separate public API layer in MVP).

2. Target cloud  
   Azure as the primary cloud provider.

3. Payment provider  
   Przelewy24 as the payment provider, used in a marketplace model with escrow-like flow supported at the application level.

4. Commission model  
   Default commission is 1%. Commission rate is configurable by admin via platform settings.

5. VAT and invoicing  
   Invoices are generated by Mercato in the system. The invoice is issued and sent to the buyer by the seller.

6. Payout schedule  
   Weekly payouts to sellers. No on-demand payouts in MVP.

7. Shipping model (MVP)  
   Shipping method is defined by the seller. Default shipping provider is InPost.

8. Returns policy  
   Buyer returns products using InPost. Mercato provides the workflow only in MVP.

9. Product taxonomy  
   Category tree is created and managed by admin.

10. Search  
    Database-level search is acceptable for MVP. Dedicated search engine is deferred.

11. Identity and 2FA  
    2FA is not required for admin or seller in MVP.

12. Data retention  
    No data deletion policy in MVP. Orders, invoices, logs, and audit data are retained indefinitely.

13. Multi-language and multi-currency  
    Multi-language support is required. Currency is limited to PLN in MVP.

14. Content moderation  
    Minimal moderation by admin: product name, description, price, and photos.

15. KPIs and SLA  
    Target uptime is 90%. Availability is handled at infrastructure level by Azure.

## 15. Seller Logistics CSV Export

1. Sellers can export their sub-orders from the order list with filters for status, date range, buyer ID, and an option to include only orders without tracking numbers.
2. Exports are limited to 5,000 sub-orders per download; sellers are prompted to narrow filters if the limit is exceeded.
3. CSV columns: OrderId, SubOrderId, SellerId, OrderCreatedAt (UTC), Status, BuyerId, BuyerName, DeliveryLine1, DeliveryLine2, DeliveryCity, DeliveryRegion, DeliveryPostalCode, DeliveryCountryCode, DeliveryPhone, ShippingMethod, ShippingCost, ItemsSubtotal, OrderTotal, TrackingNumber, TrackingCarrier, TrackingUrl, Items (semicolon-separated `ProductName (SKU: SKU) xQuantity`).
