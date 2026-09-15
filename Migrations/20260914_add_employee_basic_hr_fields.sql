-- Dados básicos e puramente informativos de RH no cadastro existente de funcionários.
ALTER TABLE employees
    ADD COLUMN JobTitle VARCHAR(150) NULL AFTER Name,
    ADD COLUMN BaseSalary DECIMAL(10,2) NULL AFTER JobTitle,
    ADD COLUMN WorkScheduleNote TEXT NULL AFTER BaseSalary;