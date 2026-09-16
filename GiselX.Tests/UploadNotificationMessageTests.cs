using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class UploadNotificationMessageTests
{
    [Fact]
    public void A_sellout_upload_renders_the_agreed_summary()
    {
        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", Settings());

        Assert.Equal("[Sellout Report] Upload Data Berhasil - Minggu 06 Juli 2026", rendered.Subject);
        Assert.Contains("Yth. Cahaya Setia Utama,", rendered.Body);
        Assert.Contains("RINGKASAN UPLOAD", rendered.Body);
        Assert.Contains("Nama File         : sellout-juli.xlsx", rendered.Body);
        Assert.Contains("Periode Data      : Minggu 06 Juli 2026", rendered.Body);
        Assert.Contains("Total Baris Data  : 1.240", rendered.Body);
        Assert.Contains("Jumlah Outlet     : 87", rendered.Body);
        Assert.Contains("Status            : ✅ Berhasil", rendered.Body);
        Assert.Contains("Diunggah oleh     : budi.csu", rendered.Body);
        Assert.Contains("Mohon tidak membalas email ini.", rendered.Body);
    }

    [Fact]
    public void The_timestamp_is_shown_in_the_reader_s_own_zone()
    {
        // Stored in UTC; an Indonesian reader seeing 03:00 for a 10:00 upload would read it as a bug.
        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", Settings());

        Assert.Contains("Waktu Upload      : 10:30 WIB", rendered.Body);
        Assert.Contains("Tanggal Upload    : 15 September 2026", rendered.Body);
    }

    [Fact]
    public void An_unknown_time_zone_falls_back_to_utc_instead_of_failing_the_notification()
    {
        var settings = Settings();
        settings.TimeZoneId = "Mars Standard Time";

        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", settings);

        Assert.Contains("Waktu Upload      : 03:30 UTC", rendered.Body);
    }

    [Fact]
    public void A_stock_upload_omits_outlets_because_it_has_none()
    {
        var rendered = UploadNotificationMessage.ForDistributor(Stock(), "Anak Mas", Settings());

        Assert.Equal("[Stock Report] Upload Data Berhasil - Snapshot 10 Agustus 2026", rendered.Subject);
        Assert.DoesNotContain("Jumlah Outlet", rendered.Body);
        Assert.Contains("Jumlah Produk     : 312", rendered.Body);
    }

    [Fact]
    public void A_partial_upload_says_so_in_the_subject_and_names_what_was_lost()
    {
        var notification = Sellout();
        notification.ValidRows = 1_200;
        notification.InvalidRows = 40;

        var rendered = UploadNotificationMessage.ForDistributor(notification, "Cahaya Setia Utama", Settings());

        Assert.StartsWith("[Sellout Report] Upload Data Berhasil Sebagian", rendered.Subject);
        Assert.Contains("Baris Ditolak     : 40", rendered.Body);
        Assert.Contains("⚠️ Berhasil sebagian (40 baris ditolak)", rendered.Body);
        Assert.Contains("TIDAK tersimpan di sistem", rendered.Body);
        // The clean-run sentence must not also appear, or the mail contradicts itself.
        Assert.DoesNotContain("kini sudah tersedia", rendered.Body);
    }

    [Fact]
    public void The_result_link_points_at_the_matching_screen()
    {
        var sellout = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", Settings());
        var stock = UploadNotificationMessage.ForDistributor(Stock(), "Anak Mas", Settings());

        Assert.Contains("https://giselx-dev.gandummas.co.id/SalesTransactions/UploadResult?groupId=4512",
            sellout.Body);
        Assert.Contains("https://giselx-dev.gandummas.co.id/Stocks/Result?batchId=901", stock.Body);
    }

    [Fact]
    public void An_archived_upload_offers_a_link_to_the_file_itself()
    {
        var notification = Sellout();
        notification.SourceFileArchived = true;

        var rendered = UploadNotificationMessage.ForDistributor(notification, "Cahaya Setia Utama", Settings());

        Assert.Contains("mengunduh kembali file yang Anda unggah", rendered.Body);
        Assert.Contains("https://giselx-dev.gandummas.co.id/SalesTransactions/DownloadSource?groupId=4512",
            rendered.Body);
    }

    [Fact]
    public void An_upload_made_while_archiving_was_off_offers_no_file_link()
    {
        // The link would 404: there is nothing behind it for that upload.
        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", Settings());

        Assert.DoesNotContain("mengunduh kembali", rendered.Body);
        Assert.DoesNotContain("DownloadSource", rendered.Body);
    }

    [Fact]
    public void A_stock_file_link_points_at_the_stock_download()
    {
        var notification = Stock();
        notification.SourceFileArchived = true;

        var rendered = UploadNotificationMessage.ForDistributor(notification, "Anak Mas", Settings());

        Assert.Contains("https://giselx-dev.gandummas.co.id/Stocks/DownloadSource?batchId=901", rendered.Body);
    }

    [Fact]
    public void An_unconfigured_dashboard_omits_the_link_rather_than_sending_a_broken_one()
    {
        var settings = Settings();
        settings.DashboardBaseUrl = "";

        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", settings);

        Assert.DoesNotContain("tautan berikut", rendered.Body);
        Assert.DoesNotContain("http", rendered.Body);
    }

    [Fact]
    public void An_unconfigured_support_contact_omits_the_sentence()
    {
        var settings = Settings();
        settings.SupportEmail = "";
        settings.SupportPhone = "";

        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", settings);

        Assert.DoesNotContain("hubungi tim support", rendered.Body);
        Assert.Contains("Terima kasih atas kerja sama Anda.", rendered.Body);
    }

    [Fact]
    public void Only_the_contact_that_is_configured_is_quoted()
    {
        var settings = Settings();
        settings.SupportPhone = "";

        var rendered = UploadNotificationMessage.ForDistributor(Sellout(), "Cahaya Setia Utama", settings);

        Assert.Contains("di support@gandummas.co.id.", rendered.Body);
        Assert.DoesNotContain("021-5555-1234", rendered.Body);
    }

    [Fact]
    public void The_internal_copy_names_the_distributor_and_drops_the_customer_wording()
    {
        var rendered = UploadNotificationMessage.ForInternalTeam(Sellout(), "Cahaya Setia Utama", Settings());

        Assert.Equal("[Sellout Report] Upload Data Berhasil - Cahaya Setia Utama - Minggu 06 Juli 2026",
            rendered.Subject);
        Assert.Contains("Distributor Cahaya Setia Utama baru saja menyelesaikan upload", rendered.Body);
        Assert.Contains("Distributor       : Cahaya Setia Utama", rendered.Body);
        // Support and thanks are addressed to the distributor, not to PT. GMK's own staff.
        Assert.DoesNotContain("hubungi tim support", rendered.Body);
        Assert.DoesNotContain("Terima kasih atas kerja sama Anda.", rendered.Body);
    }

    [Fact]
    public void A_service_level_upload_omits_the_period_it_does_not_have()
    {
        var notification = new UploadNotificationDto
        {
            CompanyId = 2,
            Kind = UploadNotificationKind.ServiceLevel,
            TotalRows = 500,
            ValidRows = 500,
            UploadedBy = "budi.csu",
            CompletedAtUtc = new DateTime(2026, 9, 15, 3, 30, 0, DateTimeKind.Utc)
        };

        var rendered = UploadNotificationMessage.ForDistributor(notification, "Cahaya Setia Utama", Settings());

        Assert.Equal("[Service Level Report] Upload Data Berhasil", rendered.Subject);
        Assert.DoesNotContain("Periode Data", rendered.Body);
        Assert.DoesNotContain("Nama File", rendered.Body);
        // No batch to link to, so no link at all rather than one pointing nowhere.
        Assert.DoesNotContain("tautan berikut", rendered.Body);
    }

    private static EmailSettings Settings() => new()
    {
        FromName = "GiselX System",
        DashboardBaseUrl = "https://giselx-dev.gandummas.co.id/",
        SupportEmail = "support@gandummas.co.id",
        SupportPhone = "021-5555-1234",
        TimeZoneId = "SE Asia Standard Time"
    };

    private static UploadNotificationDto Sellout() => new()
    {
        CompanyId = 2,
        Kind = UploadNotificationKind.Sellout,
        Reference = 4512,
        TotalRows = 1_240,
        ValidRows = 1_240,
        InvalidRows = 0,
        UploadedBy = "budi.csu",
        CompletedAtUtc = new DateTime(2026, 9, 15, 3, 30, 0, DateTimeKind.Utc),
        OriginalFileName = "sellout-juli.xlsx",
        PeriodLabel = "Minggu 06 Juli 2026",
        OutletCount = 87,
        ProductCount = 45
    };

    private static UploadNotificationDto Stock() => new()
    {
        CompanyId = 1,
        Kind = UploadNotificationKind.Stock,
        Reference = 901,
        TotalRows = 312,
        ValidRows = 312,
        InvalidRows = 0,
        UploadedBy = "sari.am",
        CompletedAtUtc = new DateTime(2026, 9, 15, 3, 30, 0, DateTimeKind.Utc),
        OriginalFileName = "stock-10-agustus.xlsx",
        PeriodLabel = "Snapshot 10 Agustus 2026",
        ProductCount = 312
    };
}
