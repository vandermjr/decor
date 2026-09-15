-- Fase P6 Passo 5.3: vincula a baixa de contas a pagar ao caixa.
ALTER TABLE accounts_payable
    ADD COLUMN IF NOT EXISTS PaidFromCashAccountID INT NULL,
    ADD KEY IF NOT EXISTS IX_accounts_payable_PaidFromCashAccountID (PaidFromCashAccountID),
    ADD CONSTRAINT FK_accounts_payable_cash_accounts
        FOREIGN KEY (PaidFromCashAccountID) REFERENCES cash_accounts (CashAccountID);
