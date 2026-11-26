namespace Hlibz.Results.AspNetCore.Tests.Admin;

[Error("admin_1001", ResultStatus.Forbidden, "Only for admins.")]
public sealed record AdminOnly : Error;
