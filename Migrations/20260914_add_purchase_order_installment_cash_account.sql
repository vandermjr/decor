-- Fase P6 Passo 5.2: vincula pagamentos de parcelas de compra ao caixa.
ALTER TABLE purchase_order_installments
    ADD COLUMN IF NOT EXISTS PaidFromCashAccountID INT NULL,
    ADD KEY IF NOT EXISTS IX_purchase_order_installments_PaidFromCashAccountID (PaidFromCashAccountID),
    ADD CONSTRAINT FK_purchase_order_installments_cash_accounts
        FOREIGN KEY (PaidFromCashAccountID) REFERENCES cash_accounts (CashAccountID);