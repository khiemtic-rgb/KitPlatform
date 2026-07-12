# Novixa Clinic (ClinicOS) — Solution Pack

Additive pack tables: `pack_clinic.*`, `pack_crm.*`. No changes to `kit_*` kernel schemas.

## GĐ1 (in progress)

- **CL1.0:** Admin `/clinic` · patients · providers · DEMO_CLINIC modules  
- Brief: `docs/novixa/03-solution/novixa-clinic-gd1-brief.md`

## Projects

| Project | Role |
|---------|------|
| `KitPlatform.Packs.Clinic.Application` | Contracts + `ClinicPackDefinition` |
| `KitPlatform.Packs.Clinic.Infrastructure` | Repos/services + `AddClinicPack` |

Migration base: `078_pack_clinic_crm.sql` · GĐ1 enable: `112_clinic_gd1_demo_clinic_modules.sql`
