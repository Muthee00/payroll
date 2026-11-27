namespace PayMaster.Tests.TestSupport;

/// <summary>
/// MySQL-backed test classes run serially: the MySQL driver's schema/connection setup is not safe
/// when several databases are created concurrently.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MySqlCollection
{
    public const string Name = "MySql";
}
