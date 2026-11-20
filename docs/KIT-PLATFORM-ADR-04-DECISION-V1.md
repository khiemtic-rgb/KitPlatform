# ADR-04 — TENANT-OWNED PRODUCTION CREDENTIALS

Status: DECIDED

Decision: Tenant-owned Production AI credentials

Record: KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md  
Analysis: KIT-PLATFORM-ADR-04-CREDENTIAL-OWNERSHIP-DECISION-BRIEF-V1.md  
Forensic: PRODUCTION-CREDENTIAL-BOUNDARY-01 — VIOLATION FOUND  
Master Architecture: V1

Secret storage: **DECIDED** — Option A. Credential ciphertext in the application database. Data-encryption key outside the database (`/etc/kit-platform/api.env` on the current VPS). External secret manager is not selected.  
Implementation of the original decision checkpoint: the line here used to say NOT STARTED.  
Current distinction: the ownership decision stays **DECIDED**. Whether Production credential gates A–D are implemented, and whether the credential regressions cited by this file returned zero failures, is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite. `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` is not in that commit and is not evidence of a live cross-product key read. Secret values are not recorded in this file.  
Closure: PRODUCTION-CREDENTIAL-STORAGE-DECISION-01-CLOSURE

---

## Decision

ADR-04 is **DECIDED**.

**Tenant-owned Production AI credentials.**

Formal statement:

> Production AI credentials are owned by Tenant.
>
> A credential used by official Production execution must be attributable to exactly one Tenant.
>
> Production must not resolve an AI credential solely from global Content settings, organization-global settings, process environment, provider name, model name, endpoint, execution permit, product pack, or Workspace, unless that mechanism is itself explicitly bound to the authorized Tenant through this credential-ownership architecture.
>
> Tenant is the mandatory ownership boundary. Workspace is not a credential ownership boundary. Product Pack does not own the credential. Provider does not own the credential.

This decision records the target. It does not claim the running code already behaves this way.

---

## Current state vs target

| | Current state | Target decision | Implementation |
|---|---|---|---|
| Ownership | One unscoped Content credential. `pack_content.org_settings` has one row and no `tenant_id`. Runway, Fal, ElevenLabs, and Veo Join resolve from that row and from process environment. Credential resolution does not receive TenantId. The structural gap is that this `org_settings` path does not separate tenants. This cell does not record an observed cross-tenant secret exposure. | Exactly one Tenant owns each Production AI credential. | Not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. |
| Identity | `ProductionAiSelection` stores ProviderId and ModelId. The frozen snapshot stores non-secret CredentialId and CredentialVersionId when freeze supplies them. | A non-secret credential identity, distinct from ProviderId, ModelId, endpoint, permit, cost, and TenantId. | Not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. |
| Freeze | Official AI freeze through a wired resolver requires an explicit CredentialId and CredentialVersionId. The snapshot stores those non-secret ids and includes them in SnapshotHash. Historical snapshots stay NULL. | Frozen execution retains a non-secret credential identity and version. | Not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. |
| Retry | Retry of a failed task reads the frozen snapshot and resolves that CredentialId and CredentialVersionId. | Retry uses the frozen reference. If that version is unusable, fail closed. | Not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. Credential business rotation remains Gate E. |
| Gemini | The same org row is readable by Pharmacy, Sales, and Local OS. | Cross-product read of a global Content setting is not an acceptable ownership model. | Not started |

The current-state column records the unscoped credential. That recorded state is the structural gap on `org_settings`. It is not an observed cross-tenant secret exposure. This document does not erase it. The name PRODUCTION-CREDENTIAL-BOUNDARY-01 is not a tracked path in commit `b64f6772213dd0e095a49ae9a84983b37116f696` and is not the evidence of that violation. This sentence does not assert that the violation is absent.

---

## Credential identity

A Production credential has a non-secret identity.

That identity is distinguishable from:

- ProviderId
- ModelId
- Endpoint
- ExecutionPermitId
- Cost
- TenantId itself

The identity may be referenced from frozen execution metadata, provenance, audit, and diagnostics.

The secret itself must not be stored in those records.

This checkpoint does not name a database table and does not name a code type.

ProviderId and ModelId identify capability and selection. They are not the secret identity.

An execution permit authorizes a provider, model, and action. It is not a credential.

---

## Credential version

A credential may have versions.

Credential identity and credential version are distinct. A version is non-secret metadata.

Rotation must be representable without silently changing the credential already frozen for a Production execution.

---

## Freeze

When official Production execution is frozen, the frozen contract retains a non-secret reference to the authorized credential identity and version used for that execution.

ProviderId plus ModelId are not sufficient to establish which credential was used.

The frozen reference must not contain an API key, bearer token, PEM, JWT, or other secret value.

---

## Retry

A retry of a frozen Production execution must not silently resolve a different credential because org settings changed, environment variables changed, a new credential was created, or an old credential was rotated.

Retry respects the credential reference and version frozen for that execution.

If that frozen version is unavailable, unauthorized, revoked, or otherwise unusable, the execution fails closed.

It does not switch to another credential, refreeze, or mutate the frozen contract.

---

## Rotation

Credential rotation is required as a capability.

A new credential version for a Tenant does not rewrite existing frozen executions.

New executions may use the newly authorized version.

Existing frozen executions keep their frozen credential reference and version.

If a later policy revokes an old version, execution fails closed rather than silently switching credentials.

---

## Authorization

Credential use requires both:

1. The Production caller is authorized for the Tenant.
2. The credential belongs to that Tenant and is authorized for the requested Production capability.

TenantId supplied by the client is not ownership authority. `ITenantContext` remains authoritative.

A CredentialId supplied by the client does not grant access to another Tenant's credential.

An identifier alone is not authorization.

---

## Product Pack boundary

Product Packs determine which provider and model capabilities a product may use. They do not own credentials.

| Concept | Role |
|---|---|
| Product Pack | Capability policy |
| Tenant | Credential ownership |
| Production selection | Provider and model capability |
| Credential | Authorized tenant-owned secret reference and version |
| Execution permit | Authorization to perform a provider, model, and action |

These stay distinct.

---

## Secret storage

**DECIDED. Option A — encrypted database secret storage.**

The secret value is ciphertext in the application database. The data-encryption key is stored outside the database. On the current VPS that file is `/etc/kit-platform/api.env`. An external secret manager is not selected.

This records the storage architecture. It does not create a table, choose a cipher, rotate a key, or migrate a secret. Running code is still the unscoped plaintext path. PRODUCTION-CREDENTIAL-BOUNDARY-01 remains VIOLATION FOUND.

Invariants:

1. Credential ciphertext is stored in the application database.
2. The data-encryption key is stored outside the database, currently at `/etc/kit-platform/api.env`.
3. Tenant ownership remains the authoritative ownership boundary.
4. CredentialId and CredentialVersion are non-secret identity.
5. Frozen executions pin CredentialId and CredentialVersion.
6. Retry resolves exactly the frozen credential version.
7. Rotation creates a new version and does not rewrite frozen executions.
8. A missing, revoked, invalid, wrong-tenant, or wrong-version credential fails closed.
9. Forbidden fallbacks: legacy `org_settings`; arbitrary environment secrets; another tenant credential; another credential version; another product or provider credential.
10. Product Pack defines capability and policy, not credential ownership.
11. Provider, Model, Endpoint, and Permit are not credential identities.
12. Provenance may store CredentialId, CredentialVersion, ProviderId, and ModelId. It must not store secret material, the encryption key, or a decrypted payload.
13. External secret manager is not selected.
14. Concrete cipher selection and master-key rotation design remain implementation Gate B.
15. Backup and restore of `/etc/kit-platform/api.env` remain a Gate B implementation concern.
16. Legacy `pack_content.org_settings` stays transitional. It is not migrated here. Tenant ownership of those values is not proven.

One application key does not replace tenant authorization. Retrieval still requires `ITenantContext`, credential ownership, and capability authorization.

---

## Legacy org_settings

`pack_content.org_settings` currently holds unscoped credentials.

ADR-04 does not declare those existing values tenant-owned. They are legacy unscoped credentials.

This decision does not:

- assign them to a Tenant
- backfill TenantId by assumption
- copy them into new credentials
- treat the Content Park singleton as Production credential authority after implementation begins

Moving or retiring them is a later data-migration concern. Ownership must be proven before any migration.

---

## Cross-product

A credential owned by Tenant A is not readable merely because another product can read the same global Content setting.

Cross-product access requires Tenant ownership, authorization, and product capability policy.

Gemini is included. The current cross-product Gemini read is evidence of the violation. It is not evidence that global credential sharing is acceptable.

---

## Provenance

Production provenance may record credential identity, credential version, provider, model, and other non-secret metadata.

Production provenance must not record API keys, access tokens, JWT secrets, PEM or private-key contents, bearer tokens, raw secret payloads, the data-encryption key, or a decrypted payload.

---

## Identities that stay separate

TenantId, CredentialId, CredentialVersion, ProviderId, ModelId, Endpoint, ExecutionPermitId, Cost, ArtifactId, StorageKey, and ByteSha256 do not substitute for one another.

---

## What this decision does not do

- Create a credential table
- Create a credential vault or select an external secret manager
- Choose a concrete cipher or implement encryption
- Implement master-key rotation or backup/restore of `/etc/kit-platform/api.env`
- Migrate `org_settings`
- Change provider adapters, Production selection, task, snapshot, attempt, artifact, or binding
- Open episode assembly
- Create an AI gateway, agent runtime, context engine, or evaluation engine
- Move credentials into KIT Core
- Create a Workspace credential scope

---

## Implementation gates

Secret storage remains **DECIDED** Option A. That decision is not reopened.

| Gate | Scope |
|---|---|
| A | Credential domain and tenant ownership. Named checkpoint: PRODUCTION-CREDENTIAL-DOMAIN-01. Presence in commit `b64f6772213dd0e095a49ae9a84983b37116f696` is not established. No provider resolution change. |
| B | Encrypted database secret storage. Named checkpoint: PRODUCTION-CREDENTIAL-STORAGE-IMPLEMENTATION-01. Presence in that commit is not established. Recorded design: AES-256-GCM envelope on the credential version. Data-encryption key name: `ProductionCredentials__DataEncryptionKey`. Not connected to provider transports. |
| C | Production credential resolution. Named checkpoint: PRODUCTION-CREDENTIAL-RESOLUTION-01. Presence in that commit is not established. Recorded design: explicit CredentialId and CredentialVersion only. Official transports fail closed without that reference. No latest-version rule. Freeze pinning is Gate D. |
| D | Freeze of credential identity and version. Named checkpoint: PRODUCTION-CREDENTIAL-FREEZE-01. Presence in that commit is not established. Recorded design: the snapshot stores CredentialId and CredentialVersionId. Historical rows are not backfilled. |
| E | Credential business rotation. Not started. |
| F | Legacy `org_settings` migration or retirement, only after ownership is proven. Not started. |

The paragraphs below record the gate design. Whether that design is present in commit `b64f6772213dd0e095a49ae9a84983b37116f696` is not established by a tracked source. This sentence does not assert the opposite. It does not reopen secret-storage Option A, and it does not authorize a Credential Service, a vault, or a platform credential plane.

The recorded Gate A design names `pack_content.production_credentials` and `pack_content.production_credential_versions`. A credential has one mandatory Tenant. A version belongs to one credential. ProviderId and ModelId on a version are metadata.

The recorded Gate B design stores an AES-256-GCM envelope on that version: envelope version `1`, non-secret `secret_key_id`, 12-byte nonce, ciphertext, and 16-byte tag. The 32-byte key name is `ProductionCredentials__DataEncryptionKey` with id `ProductionCredentials__DataEncryptionKeyId`. Previous master keys may be named `ProductionCredentials__Keys__{keyId}` so an old envelope can still be decrypted. That is master-key support only. Credential version rotation remains Gate E. The recorded design keeps the key out of PostgreSQL and out of the `pg_dump`. The recorded script names are `backup-production-credential-key.sh` and `check-production-credential-key.sh`.

The recorded boundary note named PRODUCTION-CREDENTIAL-BOUNDARY-01 says legacy Content Park and Kit Video paths can still read `org_settings`, and that official Production transports do not. Whether that note is a tracked source in the commit above is not established. This sentence does not assert the opposite.

The recorded Gate C design resolves an explicit CredentialId and CredentialVersion for the caller tenant, then decrypts with Gate B. The recorded design requires that reference for official Runway, Fal, and ElevenLabs transports and does not read `org_settings` on those paths. The recorded design says official Veo live account loading does not read `org_settings`.

The recorded Gate D design pins CredentialId and CredentialVersionId on `ProductionTaskInputSnapshot` when freeze is given an explicit reference and a resolver. Both ids participate in `SnapshotHash` when present. A snapshot that has neither id keeps the previous hash. The recorded design copies that frozen reference into the official Runway, Fal, and ElevenLabs start paths. Retry loads the stored snapshot and resolves those ids. Historical snapshots are not rewritten. Credential business rotation remains Gate E. Next checkpoint name: PRODUCTION-CREDENTIAL-ROTATION-01.

---

## Consistency

ADR-01, ADR-02, ADR-03, ADR-05, ADR-06, ADR-07, and ADR-08 are not reopened.

ADR-03 remains tenant-only Production scope. Workspace remains not a Production boundary and, by this decision, not a credential boundary.

ADR-02's register dependency still describes the Runway key as Content-owned. That sentence is the current unscoped state. It is not the target ownership rule.

ADR-05 still says credentials are not a Core primitive. This decision does not promote them into Core and does not create a platform credential plane.
