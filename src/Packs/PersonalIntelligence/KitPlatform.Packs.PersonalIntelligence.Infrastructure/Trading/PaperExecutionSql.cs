namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal static class PaperExecutionSql
{
    public const string InsertOrder = """
        INSERT INTO pack_personal_intelligence.trading_order (
            order_id, tenant_id, person_id, trading_account_id, external_order_id,
            symbol, side, quantity, limit_price, status, source, submitted_at, decision_id
        )
        VALUES (
            @OrderId, @TenantId, @PersonId, @TradingAccountId, @ExternalOrderId,
            @Symbol, @Side, @Quantity, @Price, @Status, 'paper', NOW(), @DecisionId
        )
        ON CONFLICT (trading_account_id, external_order_id) DO NOTHING
        """;

    public const string InsertExecution = """
        INSERT INTO pack_personal_intelligence.trading_execution (
            execution_id, tenant_id, trading_account_id, order_id, external_execution_id, mode, executed_at
        )
        VALUES (
            @ExecutionId, @TenantId, @TradingAccountId, @OrderId, @ExternalExecutionId, @Mode, NOW()
        )
        ON CONFLICT (trading_account_id, external_execution_id) DO NOTHING
        """;
}
