using System.Text.RegularExpressions;
using ModMove.Infrastructure.Persistence.Seed;

namespace ModMove.IntegrationTests;

/// <summary>Master data BANK seed (R-01, K1): code = 6-digit BIN, name = short name, as the master data validator allows.</summary>
public class BankSeedDataTests
{
    [Fact]
    public void Every_bank_code_is_a_six_digit_BIN_and_unique()
    {
        Assert.NotEmpty(BankSeedData.Banks);
        Assert.All(BankSeedData.Banks, b => Assert.Matches(new Regex(@"^[0-9]{6}\z"), b.Bin));
        Assert.Equal(BankSeedData.Banks.Length, BankSeedData.Banks.Select(b => b.Bin).Distinct().Count());
    }

    [Fact]
    public void Every_bank_name_fits_the_master_data_limits_and_is_not_blank()
    {
        // master_data_items.name is VARCHAR(100); CreateMasterDataItemCommandValidator caps Name at 100.
        Assert.All(BankSeedData.Banks, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(b.Name));
            Assert.True(b.Name.Length <= 100, b.Name);
        });
    }

    [Theory]
    [InlineData("970436", "Vietcombank")]
    [InlineData("970415", "VietinBank")]
    [InlineData("970418", "BIDV")]
    [InlineData("970405", "Agribank")]
    [InlineData("970422", "MBBank")]
    public void The_common_banks_are_seeded_by_BIN(string bin, string name) =>
        Assert.Contains(BankSeedData.Banks, b => b.Bin == bin && b.Name == name);

    [Fact]
    public void The_category_matches_the_public_lookup()
    {
        Assert.Equal(ModMove.Application.Features.MasterData.Queries.GetPublicContent.PublicContentCategories.Bank, BankSeedData.Category);
    }
}

