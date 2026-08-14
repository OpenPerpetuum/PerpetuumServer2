using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Packages
{
    public interface IPackageRepository
    {
        Task<List<PackageRow>> LoadAllPackagesAsync();
        Task<List<PackageItemRow>> LoadPackageItemsAsync(int packageId);
        Task<List<PackageUsageRow>> LoadSeasonUsageAsync(int packageId);
    }

    public interface IPackageRepositoryFactory
    {
        IPackageRepository Create(ConnectionSettings connection);
    }

    public sealed class PackageRepositoryFactory : IPackageRepositoryFactory
    {
        public IPackageRepository Create(ConnectionSettings connection) => new PackageRepository(connection);
    }

    public class PackageRepository : IPackageRepository
    {
        private readonly ConnectionSettings _connection;

        public PackageRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<PackageRow>> LoadAllPackagesAsync()
        {
            var result = new List<PackageRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "    p.id, " +
                "    p.name, " +
                "    (SELECT COUNT(*) FROM packageitems pi WHERE pi.packageid = p.id) AS item_count, " +
                "    (SELECT COUNT(DISTINCT season_id) " +
                "     FROM ( " +
                "         SELECT season_id FROM season_tiers WHERE package_id = p.id " +
                "         UNION ALL " +
                "         SELECT season_id FROM season_leaderboard_rewards WHERE package_id = p.id " +
                "     ) refs " +
                "    ) AS season_count " +
                "FROM packages p " +
                "ORDER BY p.name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PackageRow
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ItemCount = reader.GetInt32(2),
                    SeasonCount = reader.GetInt32(3)
                });
            }
            return result;
        }

        public async Task<List<PackageItemRow>> LoadPackageItemsAsync(int packageId)
        {
            var result = new List<PackageItemRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, packageid, definition, quantity " +
                "FROM packageitems WHERE packageid = @packageId";
            cmd.Parameters.AddWithValue("@packageId", packageId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PackageItemRow
                {
                    Id = reader.GetInt32(0),
                    PackageId = reader.GetInt32(1),
                    Definition = reader.GetInt32(2),
                    Quantity = reader.GetInt32(3)
                });
            }
            return result;
        }

        public async Task<List<PackageUsageRow>> LoadSeasonUsageAsync(int packageId)
        {
            var result = new List<PackageUsageRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT s.id AS season_id, s.name AS season_name, s.is_active, " +
                "       'Tier' AS context, t.tier_name AS detail " +
                "FROM season_tiers t " +
                "JOIN seasons s ON s.id = t.season_id " +
                "WHERE t.package_id = @packageId " +
                "UNION ALL " +
                "SELECT s.id, s.name, s.is_active, " +
                "       'Leaderboard' AS context, " +
                "       'Rank ' + CAST(lr.rank_min AS varchar) + '-' + CAST(lr.rank_max AS varchar) AS detail " +
                "FROM season_leaderboard_rewards lr " +
                "JOIN seasons s ON s.id = lr.season_id " +
                "WHERE lr.package_id = @packageId " +
                "ORDER BY season_name, context";
            cmd.Parameters.AddWithValue("@packageId", packageId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PackageUsageRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    !reader.IsDBNull(2) && reader.GetBoolean(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4)));
            }
            return result;
        }
    }

    public record PackageUsageRow(int SeasonId, string SeasonName, bool IsActive, string Context, string Detail);
}
