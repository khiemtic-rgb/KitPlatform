namespace KitPlatform.Packs.Sales;

public static class LeadStagePolicy
{
    public static readonly string[] Forward =
    [
        "discovered", "research", "contact", "engage", "phc", "qualify", "demo", "pilot", "paid",
    ];

    public static readonly string[] LostReasons =
    [
        "not_pharmacy", "not_interested", "has_enough_pos", "no_time", "price", "competitor", "no_response", "duplicate", "other",
    ];

    public static readonly HashSet<string> PaidFactCodes = new(StringComparer.Ordinal)
    {
        "founding_month", "pilot_30d", "pilot_30d_app",
    };

    public static int PaidAmount(string factCode) => factCode switch
    {
        "founding_month" => 299_000,
        "pilot_30d" => 299_000,
        "pilot_30d_app" => 598_000,
        _ => throw new InvalidOperationException("Fact giá không được xác nhận paid."),
    };

    public static string? Deny(
        string from,
        string to,
        bool hasPhcLinkOrTask,
        bool hasPhcCompleted,
        bool hasWaiver,
        bool hasLostReason,
        string? paidFactCode)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
            return null;
        if (from == LeadRetention.Archived)
            return "archived_is_terminal";
        if (to == LeadRetention.Archived)
            return "archive_via_retention";
        if (to == "lost")
            return hasLostReason ? null : "lost_reason_required";
        if (from == "lost")
            return "lost_is_terminal";

        var iFrom = Array.IndexOf(Forward, from);
        var iTo = Array.IndexOf(Forward, to);
        if (iFrom < 0 || iTo < 0)
            return "unknown_status";
        if (Math.Abs(iTo - iFrom) != 1)
            return "skip_forbidden";
        if (to == "phc" && !hasPhcLinkOrTask && !hasWaiver)
            return "phc_link_required";
        if (to == "qualify" && !hasPhcCompleted && !hasWaiver)
            return "phc_report_required";
        if (to == "demo" && !hasPhcCompleted && !hasWaiver)
            return "phc_required";
        if (to == "paid" && (paidFactCode is null || !PaidFactCodes.Contains(paidFactCode)))
            return "paid_fact_required";
        return null;
    }
}

/// <summary>
/// A lead with any history is archived, never deleted. Only an empty lead may be removed,
/// and its business only when no other lead or signal still points at it.
/// </summary>
public static class LeadRetention
{
    public const string Archived = "archived";
    public const string Deleted = "deleted";
    public const string ArchivedDeny = "lead_archived";

    public static readonly IReadOnlyList<string> ClosedStatuses = ["lost", Archived];

    public static bool IsClosed(string? status) =>
        status is not null && ClosedStatuses.Contains(status, StringComparer.Ordinal);

    public static string Decide(bool leadHasHistory) => leadHasHistory ? Archived : Deleted;

    /// <summary>
    /// Archived is terminal for staff and agent work. A message the customer sends on their own
    /// is still stored as history, but it opens no draft or task.
    /// </summary>
    public static string? DenyWork(string? status) =>
        string.Equals(status, Archived, StringComparison.Ordinal) ? ArchivedDeny : null;

    /// <summary>
    /// Funnel and desk KPIs leave archived leads out. Safety counters (unapproved sends,
    /// blocked sends, waivers) keep them, so archiving cannot hide an incident.
    /// </summary>
    public static bool CountsInKpi(string? status) => DenyWork(status) is null;

    public static bool MayDeleteBusiness(bool leadDeleted, int remainingLeads, bool businessHasSignals) =>
        leadDeleted && remainingLeads == 0 && !businessHasSignals;
}

/// <summary>What a signed Messenger inbound may open. It never sends; a draft waits for a human.</summary>
public static class FacebookInboundRules
{
    public static (bool Draft, bool Task) Plan(string? leadStatus, bool escalate) =>
        LeadRetention.DenyWork(leadStatus) is null ? (true, escalate) : (false, false);
}

public static class FacebookOutboundRules
{
    public static string? DenyCreate(string? channel, string? direction, string? reviewStatus)
    {
        if (string.Equals(channel, "facebook", StringComparison.OrdinalIgnoreCase)
            && string.Equals(direction, "outbound", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(reviewStatus, "draft", StringComparison.OrdinalIgnoreCase))
            return "draft_only";
        return null;
    }

    public static string? Deny(
        string? channel,
        string? direction,
        string? reviewStatus,
        Guid? approvedByUserId,
        bool sentAtSet,
        string? content,
        string? approvedHash,
        string? currentHash,
        bool leadLost)
    {
        if (!string.Equals(channel, "facebook", StringComparison.OrdinalIgnoreCase))
            return "channel";
        if (!string.Equals(direction, "outbound", StringComparison.OrdinalIgnoreCase))
            return "direction";
        if (!string.Equals(reviewStatus, "approved", StringComparison.OrdinalIgnoreCase))
            return "not_approved";
        if (approvedByUserId is null || approvedByUserId == Guid.Empty)
            return "approver";
        if (sentAtSet)
            return "already_sent";
        if (string.IsNullOrWhiteSpace(content))
            return "empty";
        if (string.IsNullOrWhiteSpace(approvedHash) || !string.Equals(approvedHash, currentHash, StringComparison.Ordinal))
            return "hash";
        if (leadLost)
            return "lead_closed";
        return null;
    }

    public const string ChannelFlagKey = "KitSales:Outbound:facebook:Enabled";
    public static readonly TimeSpan ApprovalTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DispatchStaleAfter = TimeSpan.FromMinutes(2);

    public static readonly IReadOnlyList<string> DispatchOutcomeEvents =
        ["outbound_sent", "outbound_failed", "outbound_unknown", "outbound_blocked"];

    /// <summary>Gate checks that run in B1, after <see cref="Deny"/>. Missing data blocks.</summary>
    public static string? DenyDispatch(
        bool? channelEnabled,
        bool settingsEnabled,
        Guid? approvalEventId,
        DateTimeOffset? approvedAt,
        DateTimeOffset now,
        bool dispatchInFlight)
    {
        if (channelEnabled is not true)
            return "channel_off";
        if (!settingsEnabled)
            return "settings_off";
        if (approvalEventId is null || approvalEventId == Guid.Empty || approvedAt is null)
            return "approval_missing";
        if (now - approvedAt.Value > ApprovalTtl || approvedAt.Value > now + TimeSpan.FromMinutes(5))
            return "approval_expired";
        if (dispatchInFlight)
            return "dispatch_in_flight";
        return null;
    }

    public const string InboundEvent = "messenger_inbound";
    public static readonly TimeSpan MessengerWindow = TimeSpan.FromHours(24);
    public static readonly TimeSpan MessengerWindowMargin = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Messenger allows a RESPONSE only within 24h of the customer's last message. The time comes
    /// only from a signed webhook <see cref="InboundEvent"/>; a missing or implausible time blocks.
    /// </summary>
    public static string? DenyMessengerWindow(DateTimeOffset? lastInboundAt, DateTimeOffset now)
    {
        if (lastInboundAt is null || lastInboundAt.Value > now + TimeSpan.FromMinutes(5))
            return "messenger_window_unknown";
        if (now - lastInboundAt.Value > MessengerWindow - MessengerWindowMargin)
            return "messenger_window_closed";
        return null;
    }

    /// <summary>Facebook's send time when it is not later than our receipt; otherwise the receipt time.</summary>
    public static DateTimeOffset InboundAt(DateTimeOffset receivedAt, long? sentAtUnixMs)
    {
        if (sentAtUnixMs is not > 0 || sentAtUnixMs > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            return receivedAt;
        var sent = DateTimeOffset.FromUnixTimeMilliseconds(sentAtUnixMs.Value);
        return sent <= receivedAt ? sent : receivedAt;
    }

    public static string InboundKey(string? messageId, string psid, long? sentAtUnixMs)
    {
        var basis = !string.IsNullOrWhiteSpace(messageId)
            ? "mid:" + messageId.Trim()
            : sentAtUnixMs is > 0 ? $"ts:{psid.Trim()}:{sentAtUnixMs}" : null;
        return basis is null
            ? $"{InboundEvent}:{Guid.NewGuid():N}"
            : $"{InboundEvent}:{Hash(basis)}";
    }

    public const string UnknownResolvedEvent = "outbound_unknown_resolved";
    public const int ResolutionNoteMax = 500;

    /// <summary>
    /// Only the latest dispatch, only when it ended unknown and nobody resolved it yet.
    /// "Delivered" needs a note saying what was seen in the Page inbox.
    /// </summary>
    public static string? DenyResolve(
        string? latestDispatchOutcome,
        bool alreadyResolved,
        bool sentAtSet,
        bool delivered,
        string? note)
    {
        if (sentAtSet)
            return "already_sent";
        if (!string.Equals(latestDispatchOutcome, "outbound_unknown", StringComparison.Ordinal))
            return "not_unknown";
        if (alreadyResolved)
            return "already_resolved";
        if (delivered && string.IsNullOrWhiteSpace(note))
            return "note_required";
        if (note is { Length: > ResolutionNoteMax })
            return "note_too_long";
        return null;
    }

    public static string DispatchKey(Guid interactionId, Guid approvalEventId) =>
        $"outbound_dispatch:{interactionId:N}:{approvalEventId:N}";

    /// <summary>
    /// "sent" only with a 2xx and a message id; "failed" only when the request surely never left
    /// (4xx, or no connection was made); everything else is "unknown" and must not be retried.
    /// </summary>
    public static string ClassifyDispatch(int? httpStatus, bool hasMessageId, bool connectFailedBeforeSend)
    {
        if (connectFailedBeforeSend && httpStatus is null)
            return "failed";
        if (httpStatus is >= 200 and < 300)
            return hasMessageId ? "sent" : "unknown";
        if (httpStatus is >= 400 and < 500)
            return "failed";
        return "unknown";
    }

    public static string Hash(string? content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes((content ?? "").Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
