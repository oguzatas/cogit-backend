namespace backend.Domain.Enums;

public enum UserRole
{
    SuperAdmin  = 0,   // Platform-level admin — no TenantId
    TenantStaff = 1    // Tenant-scoped staff — has TenantId
    // TenantEmployee is NOT an Identity role — accessed via AccessKey magic-link only
}
