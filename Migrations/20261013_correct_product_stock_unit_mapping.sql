-- Correct units missed by the first legacy backfill after reviewing product categories.
UPDATE products AS p
JOIN unit_of_measures AS old_u ON old_u.UnitOfMeasureID = p.StockUnitID AND old_u.Code = 'UN'
LEFT JOIN subgroups AS sg ON sg.SubgroupID = p.SubgroupID
LEFT JOIN `groups` AS gr ON gr.GroupID = sg.GroupID
LEFT JOIN families AS fa ON fa.FamilyID = gr.FamilyID
LEFT JOIN classes AS cl ON cl.ClassID = fa.ClassID
JOIN unit_of_measures AS target_u ON target_u.Code = CASE
    WHEN cl.ClassName = 'Estrutural'
      AND gr.GroupName NOT IN ('Tampa', 'Painel de Divisória') THEN 'BR'
    WHEN gr.GroupName = 'Prego' THEN 'KG'
    WHEN gr.GroupName = 'Primer' THEN 'GL'
    WHEN gr.GroupName = 'Revestimento 3D' AND sg.SubgroupName = 'PVC' THEN 'M2'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%cordão de solda%' THEN 'M'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%globalfix%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '%cola adesivo p/ piso vinilico e revest. quartzolit%' THEN 'BD'
    WHEN fa.FamilyName = 'Acessórios para Cortina' AND gr.GroupName IN ('Trilho', 'Varão') THEN 'BR'
END
SET p.StockUnitID = target_u.UnitOfMeasureID
WHERE cl.ClassName = 'Estrutural'
  OR gr.GroupName IN ('Prego', 'Primer')
  OR (gr.GroupName = 'Revestimento 3D' AND sg.SubgroupName = 'PVC')
  OR LOWER(COALESCE(p.Description, '')) LIKE '%cordão de solda%'
   OR LOWER(COALESCE(p.Description, '')) LIKE '%globalfix%'
   OR LOWER(COALESCE(p.Description, '')) LIKE '%cola adesivo p/ piso vinilico e revest. quartzolit%'
  OR (fa.FamilyName = 'Acessórios para Cortina' AND gr.GroupName IN ('Trilho', 'Varão'));
