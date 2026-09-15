-- Fase P6 Passo 5.1: vincula recebimentos de parcelas de venda ao caixa.
ALTER TABLE order_installments
    ADD COLUMN IF NOT EXISTS ReceivedIntoCashAccountID INT NULL,
    ADD KEY IF NOT EXISTS IX_order_installments_ReceivedIntoCashAccountID (ReceivedIntoCashAccountID),
    ADD CONSTRAINT FK_order_installments_cash_accounts
        FOREIGN KEY (ReceivedIntoCashAccountID) REFERENCES cash_accounts (CashAccountID);