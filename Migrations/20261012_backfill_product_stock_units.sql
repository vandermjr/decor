-- Backfill legacy goods using catalog taxonomy and explicit package labels.
-- Only products without a unit are changed; product-specific units remain intact.
UPDATE products AS p
LEFT JOIN subgroups AS sg ON sg.SubgroupID = p.SubgroupID
LEFT JOIN `groups` AS gr ON gr.GroupID = sg.GroupID
LEFT JOIN families AS fa ON fa.FamilyID = gr.FamilyID
LEFT JOIN classes AS cl ON cl.ClassID = fa.ClassID
JOIN unit_of_measures AS u ON u.Code = CASE
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%cx c/%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% cx%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '%cx.%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '%cx.' THEN 'CX'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%pct%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '%pacote%' THEN 'PC'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '% saco%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% sc %'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% sc.%' THEN 'SC'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%balde%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% bd %'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% bd.%' THEN 'BD'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%galão%'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% gl %'
      OR LOWER(COALESCE(p.Description, '')) LIKE '% gl.%' THEN 'GL'
      WHEN LOWER(COALESCE(p.Description, '')) LIKE '% lata%'
        OR LOWER(COALESCE(p.Description, '')) LIKE '% lt %'
        OR LOWER(COALESCE(p.Description, '')) LIKE '% lt.%'
        OR LOWER(COALESCE(p.Description, '')) LIKE '% lt2%'
        OR LOWER(COALESCE(p.Description, '')) LIKE '% lt14%' THEN 'LT'
      WHEN cl.ClassName = 'Estrutural'
        AND gr.GroupName NOT IN ('Tampa', 'Painel de Divisória') THEN 'BR'
      WHEN gr.GroupName = 'Prego' THEN 'KG'
      WHEN gr.GroupName = 'Primer' THEN 'GL'
      WHEN gr.GroupName = 'Revestimento 3D PVC' THEN 'M2'
      WHEN LOWER(COALESCE(p.Description, '')) LIKE '%cordão de solda%' THEN 'M'
      WHEN LOWER(COALESCE(p.Description, '')) LIKE '%globalfix%'
        OR LOWER(COALESCE(p.Description, '')) LIKE '%cola adesivo p/ piso vinilico e revest. quartzolit%' THEN 'BD'
      WHEN fa.FamilyName = 'Acessórios para Cortina' AND gr.GroupName IN ('Trilho', 'Varão') THEN 'BR'
      WHEN gr.GroupName = 'Revestimento 3D' AND sg.SubgroupName = 'PVC' THEN 'M2'
    WHEN cl.ClassName = 'Têxtil'
      OR gr.GroupName IN ('Fita', 'Cordão', 'Entretela') THEN 'M'
    WHEN (gr.GroupName = 'Papel de Parede' OR fa.FamilyName = 'Papel de Parede')
      AND LOWER(COALESCE(p.Description, '')) NOT LIKE '%catalogo%' THEN 'RL'
    WHEN gr.GroupName IN ('Piso Vinílico', 'Piso Laminado', 'Piso de Borracha',
                           'Carpete em Rolo', 'Carpete em Placa', 'Carpete de Forração',
                           'Espuma Acústica', 'Manta') THEN 'M2'
    WHEN gr.GroupName IN ('Rodapé', 'Rodateto', 'Boiserie', 'Rodameio',
                           'Perfil de Arremate', 'Perfil de Emenda', 'Perfil de Escada',
                           'Perfil de Transição', 'Perfil Redutor', 'Perfil T',
                           'Guarnição', 'Meia Cana', 'Moldura Externa', 'Cantoneira',
                           'Painel Ripado', 'Forro de PVC', 'Forro Vinílico', 'Perfis Metálicos')
      OR fa.FamilyName = 'Perfis' THEN 'BR'
    WHEN gr.GroupName IN ('Lona', 'Fita Crepe') THEN 'RL'
    WHEN LOWER(COALESCE(p.Description, '')) LIKE '%kit %'
      OR gr.GroupName IN ('Persiana', 'Cortina') THEN 'KIT'
    ELSE 'UN'
END
SET p.StockUnitID = u.UnitOfMeasureID
WHERE p.StockUnitID IS NULL;
