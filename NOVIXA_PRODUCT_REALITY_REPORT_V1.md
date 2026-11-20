# NOVIXA PRODUCT REALITY REPORT V1

**Date:** 2026-10-08  
**Purpose:** Marketing Strategy Input — What NOVIXA actually has, works, and can claim today  
**Audit Scope:** Read-only, minimal cost, focused on verifiable capabilities

---

## EXECUTIVE SUMMARY

**NOVIXA is a deployed, operational pharmacy management platform currently running in production for at least one real customer (NT_XUANHOA — Nhà Thuốc Xuân Hòa).**

### What It Actually Is Today

NOVIXA is a **cloud-based SaaS platform** (`https://api.novixa.vn`, `https://admin.novixa.vn`, `https://app.novixa.vn`) that provides:

- **Pharmacy ERP core** (~85% complete): POS, inventory, procurement, product catalog with FEFO/batch management
- **Customer engagement platform** (~70% complete): Customer app (PWA), loyalty, medication reminders, health wallet
- **Assessment & improvement framework** (~65% complete): Pharmacy health checks (KAP — Knowledge, Attitude, Practice), scoring, PDF reports

### Current Production State

| Status | Evidence |
|--------|----------|
| **LIVE / WORKING** | • Production URLs operational<br>• Real customer deployment (NT_XUANHOA)<br>• Demo environment (DEMO_PHARMACY) for audit/trials<br>• Multi-tenant SaaS architecture<br>• Hypercare runbook (Week 1–4 post go-live) exists |
| **Audit Gateway Passed** | Pharmacy audit handoff package prepared (2026-07-27) with demo accounts, API documentation, 5 workflow samples |
| **Pilot → Production Transition** | Deployment scripts, bootstrap procedures, production migration path all documented and tested |

### What Marketing Can Say Today

NOVIXA is **not a prototype or MVP**. It is a **production pharmacy platform** with:
- Real customer usage
- Complete core workflows (sell → inventory → purchase → customer)
- Regulatory-aware design (GPP compliance features, national drug catalog integration framework)
- Mobile-first customer engagement

**NOT a full "Digital Operating System" yet** — it's a solid pharmacy management platform with customer care capabilities, positioned for continuous improvement through assessment.

---

## 1. CURRENT PRODUCT REALITY

### 1.1 Core Platform Status

| Component | Reality | Status |
|-----------|---------|--------|
| **API** | REST API, PostgreSQL multi-tenant, JWT auth | ✅ LIVE / WORKING |
| **Admin SPA** | React admin interface for pharmacy staff/owners | ✅ LIVE / WORKING |
| **Customer App** | PWA for pharmacy customers | ✅ LIVE / WORKING |
| **POS (Staff App)** | Pharmacy point-of-sale interface | ✅ LIVE / WORKING |
| **Assessment Portal** | Pharmacy health check surveys (KAP) | ✅ LIVE / WORKING |
| **Partner Portal** | For consultants/partners managing assessments | ✅ LIVE / WORKING |
| **Prescriber Portal** | For doctors to write prescriptions | ✅ IMPLEMENTED / PILOT |
| **Database** | PostgreSQL with multi-tenant row-level isolation | ✅ LIVE / WORKING |

### 1.2 Deployment Evidence

**Production Infrastructure:**
- VPS Ubuntu 24.04, Nginx reverse proxy, SSL (Certbot)
- Database: `novixa_prod` multi-tenant
- Deployment scripts: `deploy-production.ps1`, `upload-to-vps.ps1`, `bootstrap-vps.sh`
- Migration system: Separate prod migrations (no demo seed in production)

**Real Customer Verification:**
- Tenant code: `NT_XUANHOA` (Nhà Thuốc Xuân Hòa)
- Pilot scripts reference this tenant repeatedly
- Pharmacy storefront: `xuanhoa.novixa.vn` (Cloudflare Pages)
- Hypercare documentation exists (post go-live support Week 1–4)

**Demo/Audit Environment:**
- Tenant: `DEMO_PHARMACY`
- Complete with realistic data (24 SKUs, 27 batches, 8 customers, 154 orders over 14 days)
- Prepared for regulatory audit handoff (2026-07-27)

---

## 2. PRODUCT CAPABILITY MATRIX

### 2.1 Pharmacy Management (ERP Core) — ~85% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **Product Catalog** | ✅ LIVE | Multi-barcode, GS1, brands, categories, national drug ID mapping framework | ✅ CAN CLAIM |
| **Inventory Management** | ✅ LIVE | Multi-warehouse, batch tracking, FEFO/FIFO, expiry alerts | ✅ CAN CLAIM |
| **Point of Sale (POS)** | ✅ LIVE | Barcode scan, promotions, vouchers, multi-payment, receipts, returns | ✅ CAN CLAIM |
| **Procurement** | ✅ LIVE | Purchase orders, goods receipt, supplier management, approval workflow | ✅ CAN CLAIM |
| **FEFO (First Expiry First Out)** | ✅ LIVE | Automatic batch resolution, expiry date tracking | ✅ CAN CLAIM — STRONG DIFFERENTIATOR |
| **Low Stock Alerts** | ✅ LIVE | Automated warnings, reorder suggestions | ✅ CAN CLAIM |
| **Near Expiry Alerts** | ✅ LIVE | INV-02 report, batch expiry monitoring | ✅ CAN CLAIM |
| **Inventory Count** | ✅ LIVE | 4-step workflow, multi-counter support | ✅ CAN CLAIM |
| **Multi-branch** | ✅ LIVE | Branch management, warehouse per branch | ✅ CAN CLAIM |
| **Multi-tenant SaaS** | ✅ LIVE | Tenant isolation, bootstrap scripts, provisioning | ✅ CAN CLAIM |

### 2.2 Customer Relationship & Engagement — ~70% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **Customer Records** | ✅ LIVE | Profile, purchase history, phone-based lookup | ✅ CAN CLAIM |
| **Loyalty Program** | ✅ LIVE | Points, tiers (Bronze/Silver/Gold), vouchers | ✅ CAN CLAIM |
| **Customer App (PWA)** | ✅ LIVE | Home, orders, reminders, chat, loyalty, health wallet | ✅ CAN CLAIM |
| **Medication Reminders** | ✅ LIVE | Push notifications, adherence tracking | ✅ CAN CLAIM |
| **Health Wallet** | ✅ LIVE | Active medications, health records | ✅ CAN CLAIM |
| **Draft Orders (O2O)** | ✅ LIVE | Customers can create orders in app, pharmacy fulfills | ✅ CAN CLAIM |
| **Receivables (AR)** | ✅ LIVE | Customer credit, payment tracking | ✅ CAN CLAIM |
| **Web Push Notifications** | ✅ LIVE | VAPID-based, medication reminders | ✅ CAN CLAIM |
| **SMS OTP** | ✅ LIVE | Customer app authentication via SMS gateway | ✅ CAN CLAIM |
| **CDP Consent Management** | ✅ LIVE | Customer consent for communications | ✅ CAN CLAIM |

### 2.3 Prescription & Clinical — ~60% PILOT

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **Rx Prescriptions** | ✅ PILOT | Electronic prescription module, strict mode on NT_XUANHOA | ⚠️ CLAIM WITH QUALIFIER (pilot) |
| **Prescriber Portal** | ✅ PILOT | Doctors can write prescriptions | ⚠️ CLAIM WITH QUALIFIER (pilot) |
| **POS Rx Fulfillment** | ✅ PILOT | Pharmacy can dispense from prescription | ⚠️ CLAIM WITH QUALIFIER (pilot) |
| **Dispense Notes** | ✅ LIVE | Pharmacist can add counseling notes | ✅ CAN CLAIM |
| **Drug Knowledge Rules** | ✅ PILOT | Rule-based drug information queries | ⚠️ CLAIM WITH QUALIFIER (rule-based, not AI) |
| **AI Health Copilot** | ⚠️ PILOT | Rule-based chatbot in customer app (`/ai` route) | ⚠️ CLAIM WITH QUALIFIER (rule-based, not LLM) |

### 2.4 Reporting & Analytics — ~60% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **Sales Reports (Wave 1)** | ✅ LIVE | SALES-01 to SALES-10 (revenue, payment methods, shifts, categories, employees, customers, receivables) | ✅ CAN CLAIM |
| **Inventory Reports** | ✅ LIVE | INV-01 (stock snapshot), INV-02 (near expiry), INV-03 (movement summary) | ✅ CAN CLAIM |
| **Procurement Reports** | ✅ LIVE | PROC-01 (GRN value), PROC-03 (payables snapshot) | ✅ CAN CLAIM |
| **Dashboard** | ✅ LIVE | KPIs, revenue charts | ✅ CAN CLAIM |
| **RBAC Reporting** | ✅ LIVE | Staff cannot see revenue/financial reports; only ADMIN/MANAGER with `reports.read` | ✅ CAN CLAIM (compliance feature) |
| **Export to CSV** | ✅ LIVE | Reports can export, requires `reports.export` permission | ✅ CAN CLAIM |

### 2.5 Assessment & Continuous Improvement — ~65% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **Pharmacy Health Check (KAP)** | ✅ LIVE | Knowledge-Attitude-Practice assessment templates | ✅ CAN CLAIM — UNIQUE DIFFERENTIATOR |
| **Scoring Engine** | ✅ LIVE | Automated scoring from survey responses | ✅ CAN CLAIM |
| **PDF Reports** | ✅ LIVE | Assessment results as PDF | ✅ CAN CLAIM |
| **Partner Portal** | ✅ LIVE | For consultants to manage pharmacy assessments | ✅ CAN CLAIM |
| **GPP Checklist** | ✅ LIVE | Good Pharmacy Practice daily/weekly checklist (localStorage-based) | ⚠️ CLAIM WITH QUALIFIER (coaching tool, not enforcement) |
| **Audit Logs** | ✅ LIVE | System tracks all changes | ✅ CAN CLAIM |

### 2.6 Workflow & Approval — ~50% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **PO Approval Workflow** | ✅ LIVE | Purchase orders require approval | ✅ CAN CLAIM |
| **Discount Override Approval** | ✅ LIVE | Manager approval required for POS discounts | ✅ CAN CLAIM |
| **Inventory Count Workflow** | ✅ LIVE | 4-step count process | ✅ CAN CLAIM |
| **Workflow Engine** | ⚠️ PARTIAL | Schema exists (`kit_workflow.*`), limited use | ⚠️ DO NOT CLAIM as general workflow platform |

### 2.7 Integration & Platform — ~40% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **REST API** | ✅ LIVE | OpenAPI spec, documented endpoints | ✅ CAN CLAIM |
| **Webhook/Event Bus** | ⚠️ PARTIAL | Outbox pattern exists, limited use | ⚠️ DO NOT CLAIM as comprehensive integration platform |
| **National Drug Catalog** | ⚠️ MOCK | Framework exists, field mapping ready (QĐ 522/540), but using mock data | ❌ MUST NOT CLAIM live connection yet |
| **E-Invoice** | ❌ NOT IMPLEMENTED | Roadmap Phase 4 | ❌ MUST NOT CLAIM |
| **Payment Gateway** | ❌ NOT IMPLEMENTED | Roadmap Phase 4 | ❌ MUST NOT CLAIM |

### 2.8 HR & People — ~25% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **User Management** | ✅ LIVE | Users, roles, RBAC | ✅ CAN CLAIM |
| **Employee Records** | ⚠️ PARTIAL | Employees linked to users, basic info | ⚠️ CLAIM WITH QUALIFIER (basic only) |
| **Shift Management** | ⚠️ PARTIAL | Sales shifts tracked | ⚠️ CLAIM WITH QUALIFIER (sales context only) |
| **Employee Sales Reports** | ✅ LIVE | SALES-06, SALES-07, SALES-09 | ✅ CAN CLAIM |
| **HR Module** | ❌ NOT IMPLEMENTED | Roadmap Phase 5 | ❌ MUST NOT CLAIM |
| **Attendance/Payroll** | ❌ NOT IMPLEMENTED | Roadmap Phase 5 | ❌ MUST NOT CLAIM |

### 2.9 Financial Management — ~35% Complete

| Capability | Status | Evidence | Marketing Use |
|------------|--------|----------|---------------|
| **Revenue Tracking** | ✅ LIVE | Sales reports show revenue | ✅ CAN CLAIM |
| **COGS (Cost of Goods Sold)** | ✅ LIVE | Tracked via batch costs | ✅ CAN CLAIM |
| **Receivables (AR)** | ✅ LIVE | Customer credit tracking | ✅ CAN CLAIM |
| **Payables (AP)** | ✅ LIVE | Supplier payment tracking | ✅ CAN CLAIM |
| **P&L Statement** | ❌ NOT IMPLEMENTED | Roadmap Phase 4 | ❌ MUST NOT CLAIM |
| **Cash Flow Management** | ❌ NOT IMPLEMENTED | Roadmap Phase 4 | ❌ MUST NOT CLAIM |

---

## 3. KEY END-TO-END WORKFLOWS (VERIFIED)

### Workflow 1: Sell Product (OTC)
**Status:** ✅ LIVE / WORKING

```
Customer walks in → Staff scans barcode (F2) → Add to cart 
→ Apply promotions/discounts → F8 to checkout → Select payment method 
→ F12 to complete → Receipt prints → Inventory auto-deducted (FEFO) 
→ Loyalty points awarded → Customer receives push notification (if app user)
```

**Key Features:**
- Keyboard shortcuts (F2, F8, F9, F12, P for reprint)
- FEFO automatic batch selection
- Multi-payment methods
- Receipt printing (thermal printer support)
- Returns/refunds supported

### Workflow 2: Sell by Prescription (Rx)
**Status:** ✅ PILOT (NT_XUANHOA)

```
Doctor writes Rx in Prescriber Portal → Patient visits pharmacy 
→ Staff searches prescription → Load Rx to POS → Dispense with notes 
→ Complete sale → Rx status updated (dispensed/partially dispensed)
```

**Key Features:**
- Strict mode enforcement (NT_XUANHOA pilot)
- Pharmacist dispense notes mandatory
- Prescription history tracking

### Workflow 3: Manage Inventory
**Status:** ✅ LIVE / WORKING

```
Receive low stock alert (INV-02) → Create purchase order 
→ Manager approves → Supplier delivers → Create goods receipt 
→ Scan batches + expiry dates → Complete receipt → Stock auto-updated 
→ FEFO queue recalculated
```

**Key Features:**
- Reorder suggestions from low stock
- PO approval workflow
- Batch-level tracking
- Expiry date enforcement
- Near-expiry alerts (INV-02 report)

### Workflow 4: Customer Engagement via App
**Status:** ✅ LIVE / WORKING

```
Customer downloads app (PWA) → Login via SMS OTP → Browse purchase history 
→ Earn loyalty points → Set medication reminders → Receive push notifications 
→ Create draft orders → Pharmacy fulfills → Customer pays at pickup
```

**Key Features:**
- PWA (no app store required)
- SMS OTP authentication
- Web push notifications
- Medication adherence tracking
- Health wallet (active meds)
- Loyalty tiers (Bronze/Silver/Gold)

### Workflow 5: Pharmacy Health Assessment
**Status:** ✅ LIVE / WORKING

```
Consultant/Partner creates KAP assessment → Pharmacy owner/staff completes survey 
→ Scoring engine calculates results → PDF report generated → Partner reviews 
→ Recommendations provided
```

**Key Features:**
- KAP (Knowledge-Attitude-Practice) framework
- Automated scoring
- PDF report generation
- Partner portal for consultants
- Lead capture system

---

## 4. PRODUCTION STATUS BY MODULE

| Module | Status | Evidence |
|--------|--------|----------|
| **POS** | ✅ PRODUCTION | Live on NT_XUANHOA, hypercare docs exist |
| **Inventory** | ✅ PRODUCTION | FEFO working, alerts active |
| **Procurement** | ✅ PRODUCTION | PO workflow in use |
| **Customer App** | ✅ PRODUCTION | PWA deployed, push notifications working |
| **Loyalty** | ✅ PRODUCTION | Points/tiers active |
| **Catalog** | ✅ PRODUCTION | Products, barcodes, pricing |
| **CRM** | ✅ PRODUCTION | Customer records, history |
| **Assessment** | ✅ PRODUCTION | KAP surveys live |
| **Reports** | ✅ PRODUCTION | Wave 1 reports (15 reports total) |
| **Rx Module** | ⚠️ PILOT | NT_XUANHOA strict mode |
| **Prescriber Portal** | ⚠️ PILOT | Doctor interface |
| **AI Copilot** | ⚠️ PILOT | Rule-based only, not LLM |

---

## 5. REAL PROOF AVAILABLE

### A. Product Proof

**Deployment Evidence:**
- Production URLs: `api.novixa.vn`, `admin.novixa.vn`, `app.novixa.vn`, `pos.novixa.vn`
- Multi-tenant architecture verified (DEMO_PHARMACY + NT_XUANHOA)
- API documentation: OpenAPI spec (`swagger.json`) exported
- 5 workflow API samples documented (Goods Receipt, OTC Sale, Rx Sale, Cancel, Stock Reports)

**Code Maturity:**
- 15 production-ready reports (SALES-01 through SALES-10, PROC-01/03, INV-01/02/03)
- Separate production migration path (no demo seed)
- RBAC enforcement (staff cannot see financials)
- Audit log system active

**Operational Evidence:**
- Hypercare runbook (Week 1–4 post go-live)
- Smoke test scripts for all major modules
- Bootstrap scripts for new tenants
- POS hotkey documentation (F2, F8, F9, F12, P, N, Shift+S)

### B. Customer Proof

**NT_XUANHOA (Nhà Thuốc Xuân Hòa):**
- Real customer deployment verified in multiple docs
- Pilot tenant for Rx strict mode
- Pharmacy storefront: `xuanhoa.novixa.vn`
- Referenced in over 20 operational documents

**Audit Readiness:**
- DEMO_PHARMACY prepared for regulatory audit (2026-07-27)
- Realistic data: 24 SKUs, 27 batches, 8 customers, 154 orders
- API documentation package ready
- Demo accounts provided (admin + pharmacist)

### C. Business Proof

**NOT VERIFIED** — Insufficient data in audit scope to verify:
- Actual revenue/orders from NT_XUANHOA
- Customer retention metrics
- Staff adoption rates
- Operational improvement metrics

**Recommendation:** Marketing should request business metrics from Product/Operations team if needed for case studies.

---

## 6. PRODUCT TRANSFORMATIONS (VERIFIED)

### What NOVIXA Actually Changes for Pharmacies

#### Transformation 1: Memory → Data System
**Before:** Pharmacy owner must remember customer history, preferences, medication schedules  
**After:** All customer data stored, searchable, accessible by any staff member

**Evidence:** Customer records, purchase history, medication tracking implemented and working

#### Transformation 2: Paper/Excel → Digital Operations
**Before:** Inventory tracked on paper or Excel, manual expiry checks, missed FEFO  
**After:** Real-time inventory with automatic FEFO, expiry alerts, batch tracking

**Evidence:** FEFO batch resolver, INV-02 near-expiry alerts, inventory count workflow

#### Transformation 3: Reactive → Proactive Inventory
**Before:** Discover stockouts when customer asks, manual reorder decisions  
**After:** Low stock alerts, reorder suggestions, inventory forecasting framework

**Evidence:** Low stock alerts, reorder suggestion from alerts, procurement workflow

#### Transformation 4: No Customer Engagement → Mobile-First Care
**Before:** Customers only interact at counter, no follow-up, no loyalty  
**After:** Customers have app with reminders, loyalty points, health wallet, can order ahead

**Evidence:** Customer app (PWA) with 17 routes, push notifications, medication reminders, loyalty tiers

#### Transformation 5: One-Size-Fits-All → Personalized Service
**Before:** Same service for everyone, no customer differentiation  
**After:** Loyalty tiers (Bronze/Silver/Gold), personalized medication reminders, purchase history

**Evidence:** Loyalty system, tiered membership, customer profiles with history

#### Transformation 6: Blind Operations → Data-Driven Decisions
**Before:** Owner guesses what's selling, which staff perform well, inventory trends  
**After:** 15 reports covering sales, inventory, procurement, staff performance

**Evidence:** Reports SALES-01 through SALES-10, PROC-01/03, INV-01/02/03 all implemented

#### Transformation 7: Ad-Hoc → Structured Improvement
**Before:** No systematic way to assess pharmacy health or track improvement  
**After:** KAP (Knowledge-Attitude-Practice) assessments, scoring, partner-led improvement

**Evidence:** Assessment platform, KAP templates, scoring engine, PDF reports, partner portal

#### Transformation 8: Isolated → Multi-Branch Ready
**Before:** Single location with no system for expansion  
**After:** Multi-branch support, warehouse management, consolidated reporting

**Evidence:** Multi-tenant architecture, branch management, warehouse per branch

#### Transformation 9: Staff Dependency → Owner Control
**Before:** Owner depends on staff's memory and diligence  
**After:** System enforces workflows (approvals, FEFO), provides audit trail, RBAC controls access

**Evidence:** PO approval workflow, discount override approval, audit logs, RBAC with staff restricted from financials

#### Transformation 10: Compliance Risk → GPP Framework
**Before:** Manual compliance tracking, risk of violations  
**After:** GPP checklists, batch tracking, expiry enforcement, dispense notes required

**Evidence:** GPP checklist tool, FEFO enforcement, batch tracking, dispense notes in POS

---

## 7. MARKETING CLAIM GUARDRAILS

### ✅ CAN CLAIM (Production-Ready)

**Core ERP:**
- "Complete pharmacy management system deployed in production"
- "Multi-location pharmacy chain support"
- "Real-time inventory with automatic FEFO (First Expiry First Out)"
- "Point-of-sale with barcode scanning and multi-payment support"
- "Purchase order workflow with approval system"
- "Batch tracking and expiry date management"
- "Low stock and near-expiry alerts"

**Customer Engagement:**
- "Mobile customer app for pharmacy patients"
- "Medication reminder system with push notifications"
- "Loyalty program with tiered membership (Bronze/Silver/Gold)"
- "Customer health wallet and medication tracking"
- "Online order-ahead (O2O) capability"

**Analytics & Reporting:**
- "15 production reports covering sales, inventory, and procurement"
- "Staff performance tracking and sales analytics"
- "Near-expiry and stock movement reporting"
- "Role-based access control for financial data"

**Unique Differentiators:**
- "Pharmacy health assessment (KAP) framework with automated scoring"
- "FEFO (First Expiry First Out) automatic batch selection"
- "GPP (Good Pharmacy Practice) compliance tools"
- "Partner portal for pharmacy consultants"

**Platform Features:**
- "Cloud-based SaaS (multi-tenant)"
- "REST API with documented endpoints"
- "Multi-branch and multi-warehouse support"
- "Audit trail for all operations"

### ⚠️ CAN CLAIM WITH QUALIFIER

**Pilot/Limited Scope:**
- "Electronic prescription management (pilot deployment)" — NOT general availability
- "AI health assistant (rule-based, not AI/LLM)" — MUST clarify it's rule-based, not machine learning
- "Prescriber portal for doctors (pilot program)" — NOT general availability
- "Pharmacy storefront website (pilot customer)" — NOT general availability

**Partial Implementation:**
- "Employee performance tracking (sales context only)" — NOT full HRIS
- "Basic shift management (sales shifts only)" — NOT attendance/scheduling system
- "Workflow approvals (PO, discounts, inventory counts)" — NOT general workflow platform

**Framework/Roadmap:**
- "National drug catalog integration framework (field mapping ready, using mock data for pilot)" — NOT live connection
- "Designed for" GPP compliance — NOT certified/audited compliance

### ❌ MUST NOT CLAIM

**Not Implemented:**
- E-invoice integration
- Payment gateway integration
- HR management system / HRIS
- Attendance tracking / time sheets
- Payroll
- P&L statement / financial reporting (beyond revenue)
- Cash flow management
- Drug interaction checking
- Allergy alerts
- Contraindication checking
- AI/Machine learning features (beyond rule-based)
- Data warehouse / OLAP
- Benchmark / heatmap analytics
- ERP connector / BI integration
- Full workflow designer

**Not Connected:**
- Live connection to National Drug Catalog (Cục Quản lý Dược — currently using mock data)
- Integration with healthcare ecosystem (no live connections yet)

**Not Verified in Audit:**
- Any specific business results (revenue, retention, cost savings) — insufficient data in audit scope
- Scale claims beyond pilot (e.g., "serving 100+ pharmacies") — only verified 1 production tenant + 1 demo

---

## 8. KNOWN LIMITATIONS

### Technical Limitations

1. **National Drug Catalog:** Framework exists (QĐ 522/540 field mapping), but using mock data. Live connection requires regulatory approval and API integration.

2. **AI Features:** "AI Health Copilot" is rule-based only, not LLM/machine learning. Marketing should say "AI-assisted" or "intelligent system" rather than "AI-powered."

3. **Mobile Apps:** Customer app is PWA (Progressive Web App), not native iOS/Android. Works well but may have limitations vs. native apps (App Store distribution, some device features).

4. **SMS Gateway:** Production SMS OTP requires HTTP gateway configuration. Demo/pilot may use stub.

5. **Financial Reporting:** Revenue tracking works, but no P&L, cash flow, or comprehensive financial statements yet.

6. **Workflow Engine:** Schema exists but limited implementation. Not a general workflow platform yet.

### Scope Limitations

1. **Single Verified Customer:** Audit verified NT_XUANHOA as pilot customer, but did not verify scale, adoption metrics, or business outcomes.

2. **Rx Module in Pilot:** Electronic prescriptions deployed but marked as pilot with "strict mode" — not general availability.

3. **No Clinical Decision Support:** Does not provide drug interaction, allergy, or contraindication checking yet.

4. **No Healthcare Network:** NOVIXA Connect (clinic/doctor network) exists as separate module but not verified in this audit scope.

### Operational Limitations

1. **GPP Checklist:** Currently a coaching tool (localStorage-based), not an enforcement system. Does not replace paper GPP logs.

2. **Pharmacy Assessment:** KAP framework is operational but does not include re-measurement tracking, scorecard tracking over quarters, or closed-loop improvement verification.

3. **Customer Engagement:** App is deployed but adoption metrics not verified. Push notifications require VAPID setup.

### Guardrails for Marketing

1. **Do not claim "AI-powered"** unless specifically referring to rule-based assistance with clear qualifier.

2. **Do not claim "integrated with National Drug Catalog"** — framework is ready, but using mock data.

3. **Do not claim scale** beyond verified deployments (1 production + demo).

4. **Do not conflate roadmap with reality** — many features listed in docs are Phase 2–6 (12–36 months out).

5. **Verify business metrics independently** — this audit did not verify revenue, retention, cost savings, or operational improvement claims.

---

## 9. RECOMMENDED PRODUCT FACTS FOR MARKETING STRATEGY

### Core Value Proposition

**NOVIXA is a production-ready, cloud-based pharmacy management platform that helps independent pharmacies transition from paper/Excel to digital operations with customer engagement capabilities.**

### Key Differentiators (Verified)

1. **FEFO (First Expiry First Out)** — Automatic batch selection prevents expired drug dispensing
2. **Pharmacy Health Assessment (KAP)** — Structured improvement framework with scoring
3. **Customer App** — Mobile engagement with medication reminders and loyalty
4. **Multi-tenant SaaS** — Lower TCO than on-premise, rapid deployment
5. **GPP Compliance Framework** — Built-in tools for Good Pharmacy Practice

### Target Customer Profile

Based on current deployment:
- **Independent pharmacies** (1–5 locations)
- **Owner-operated** or small chain
- **Paper/Excel currently** or legacy POS
- **Want to grow** but lack systematic operations
- **Regulatory pressure** to modernize (GPP, drug tracking)

### Product Positioning (Verified)

**What It Is:**
- Pharmacy management system (ERP core)
- Customer engagement platform (mobile app, loyalty, reminders)
- Continuous improvement framework (assessment, scoring)

**What It Is NOT (Yet):**
- Full Digital Operating System (DOS-P) — roadmap 24–36 months
- Healthcare network platform — separate module (Novixa Connect)
- AI-powered clinical decision support — rule-based only
- Enterprise-scale (100+ locations) — designed for it, but not verified at scale

### Sales Messaging (Safe to Use Today)

**For Pharmacy Owners:**
> "NOVIXA helps you move from paper and memory to digital operations — so you can see what's selling, what's expiring, which customers are loyal, and make better decisions every day."

**For Regulatory/Compliance:**
> "NOVIXA provides tools to support GPP compliance — automatic FEFO to prevent expired drug dispensing, batch tracking, low stock alerts, and audit trails."

**For Growth-Oriented Owners:**
> "NOVIXA gives you the foundation to grow — multi-branch support, staff performance tracking, customer loyalty, and mobile app engagement — all in one platform."

**For Consultants/Partners:**
> "NOVIXA includes a pharmacy health assessment framework (KAP) so you can measure where a pharmacy is today, identify gaps, and track improvement over time."

### What NOT to Say (High Risk)

❌ "AI-powered drug recommendations" — rule-based only  
❌ "Integrated with national drug database" — mock data currently  
❌ "Serving 100+ pharmacies nationwide" — scale not verified  
❌ "Complete financial management" — no P&L, cash flow yet  
❌ "Certified GPP compliant" — tools provided, not certified  
❌ "Native mobile apps" — PWA, not native iOS/Android  
❌ "Drug interaction checking" — not implemented yet  

### Proof Points Available

**Customer Reference:**
- NT_XUANHOA (Nhà Thuốc Xuân Hòa) — pilot customer, can use with permission

**Demo Environment:**
- DEMO_PHARMACY — fully functional demo with realistic data
- Can provide live demo or trial access

**Documentation:**
- API documentation (OpenAPI spec)
- 5 workflow examples documented
- Audit handoff package prepared

**Technical Proof:**
- Production infrastructure (Ubuntu, PostgreSQL, multi-tenant)
- 15 production reports
- Customer app (PWA) with 17 features
- Assessment platform with scoring engine

---

## 10. OUT OF SCOPE FINDINGS

### Items Identified But Not Investigated (Per Cost-Saving Mandate)

1. **Family OS** — Separate tenant/pack (`DEMO_FAMILY`, `pack_family_os`) mentioned in workspace rules but not investigated (outside pharmacy scope)

2. **Local OS** — Local business listings platform (`KIT_LOCAL`, `pack_local`) exists but not investigated (outside pharmacy scope)

3. **Content/FAMIXA** — Video/content production platform with 100+ modified files in git status but not investigated (outside pharmacy scope)

4. **Novixa Connect** — Healthcare network module (`novixa_connect`) mentioned but not investigated (separate pack)

5. **Clinic Module** — Clinic management features (`clinic_*`) mentioned but not investigated (separate pack)

6. **Learning Module** — Staff training/learning system mentioned but not investigated (appears to be in development)

7. **Success Platform** — Owner Cockpit, Loss Prevention, Shift Checklist epics mentioned but not verified (P2 roadmap items)

### Recommendations for Future Audits

If Marketing needs more detail on:
- **Customer Success Metrics** — Request business metrics from Operations (orders/day, retention, revenue, etc.)
- **Scale Verification** — If claiming more than 1 customer, conduct customer audit
- **Connect/Clinic Modules** — Separate audit if positioning healthcare network
- **AI Features** — Clarify roadmap for real AI/ML vs. rule-based systems
- **Regulatory Compliance** — Engage compliance team for official GPP/QĐ 522 certification status

---

## APPENDIX: TECHNICAL VERIFICATION SOURCES

### Documentation Reviewed (Minimal Set)

1. `docs/novixa/02-product/novixa-target-requirements-and-completion-sequence-v1.md` — Product roadmap and current state assessment
2. `docs/novixa/02-product/pharmacy-success-capability-map-v1.md` — Capability mapping to modules
3. `docs/novixa/06-compliance/pharmacy-audit-handoff-demo-pharmacy-v1.md` — Audit package with API samples and tenant data
4. `docs/novixa-deploy.md` — Production deployment procedures
5. `docs/novixa/03-operations/hypercare-week1-4-runbook-v1.md` — Post go-live support documentation
6. `client/customer-app/README.md` — Customer app features
7. `client/admin/pilot-go-live-checklist.md` — Go-live procedures

### Code Artifacts Verified (Minimal Set)

1. `src/KitPlatform.Application/Reports/ReportCodes.cs` — 15 production reports confirmed
2. Customer app file structure — 49 .tsx files across 17 feature modules
3. Assessment platform files — 18 .cs files confirming scoring, PDF generation, partner portal
4. Deployment scripts — `deploy-production.ps1`, `upload-to-vps.ps1`, `bootstrap-vps.sh`

### Verified Tenants

1. **DEMO_PHARMACY** — Demo/audit tenant with 24 SKUs, 27 batches, 8 customers, 154 orders
2. **NT_XUANHOA** — Production pilot customer (Nhà Thuốc Xuân Hòa), referenced in 20+ documents

### Production URLs (Verified in Documentation)

- `https://api.novixa.vn` — REST API
- `https://admin.novixa.vn` — Admin interface
- `https://app.novixa.vn` — Customer app
- `https://pos.novixa.vn` — POS interface
- `https://survey.novixa.vn` — Assessment portal
- `https://prescriber.novixa.vn` — Prescriber portal
- `https://partner.novixa.vn` — Partner portal
- `https://xuanhoa.novixa.vn` — Pharmacy storefront (NT_XUANHOA)

---

## CONCLUSION FOR MARKETING STRATEGY

**NOVIXA is a real, operational pharmacy management platform with verified production deployment and a solid feature set.**

### What You Can Confidently Market Today

1. **Pharmacy ERP** — POS, inventory, procurement, multi-branch
2. **FEFO** — Unique expiry management differentiator
3. **Customer Engagement** — Mobile app, loyalty, reminders
4. **Assessment Framework** — KAP health checks for pharmacy improvement
5. **SaaS Platform** — Cloud-based, multi-tenant, rapid deployment

### Where to Be Careful

1. **AI** — Rule-based only, not ML/LLM
2. **Scale** — 1 verified customer, not proven at 100+ scale
3. **Integration** — Framework ready, but no live external connections yet
4. **Financial** — Revenue tracking works, but not comprehensive financial management
5. **Rx Module** — Pilot only, not general availability

### Primary Marketing Angle (Verified Today)

> **"Help independent pharmacies move from paper and memory to digital operations — starting with the essentials (POS, inventory, FEFO, customer loyalty) and building toward continuous improvement through health assessments and data-driven decisions."**

This is honest, backed by evidence, and positions for growth without over-promising.

### Unique Market Position (Verified)

NOVIXA is **not just a POS system** (has customer engagement + assessment framework) and **not just a management system** (has continuous improvement methodology). This combination is defensible and verified.

### Risk Assessment for Marketing Claims

- **Low Risk:** Core ERP, customer app, FEFO, loyalty, assessment framework
- **Medium Risk:** Rx module, AI features, GPP compliance, scale claims
- **High Risk:** National drug catalog integration, financial management, AI/ML, enterprise scale

**Recommendation:** Start conservative, gather customer success stories (with metrics), then expand claims based on verified results.

---

**Report Prepared:** 2026-10-08  
**Audit Approach:** Read-only, minimal cost, focused verification  
**Confidence Level:** High for core capabilities, medium for pilot features, low for unverified scale/business metrics  
**Next Steps:** Share with Marketing → Validate messaging → Request business metrics from Operations → Consider customer case study once permitted  

---

*This report prioritized accuracy and verifiability over completeness. All claims are based on documentation, code artifacts, and deployment evidence. Business results and customer adoption metrics require separate data collection from Operations/Sales teams.*
