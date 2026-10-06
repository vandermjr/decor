# Administracao de acesso

## Telas

- **Usuarios:** grid alinhado aos cadastros, com codigo, username (ou Administrador para a conta `admin`) e status. Criacao e edicao compartilham o formulario na mesma janela, sem modal, com acoes no rodape separado por uma linha. Pesquisa, criacao e edicao usam apenas username; o campo legado `DisplayName` continua no banco e nos contratos, preenchido com o username normalizado. Os grupos do usuario selecionado aparecem abaixo do grid em checkboxes, com Salvar grupos. Nao apresenta modulos ou permissoes individuais.
- **Grupos:** cadastro de grupos de seguranca (`roles`), com nome e descricao; o nivel hierarquico permanece nos contratos e na validacao, sem campo visivel no formulario. Nao altera permissoes. Somente Administradores (incluindo o alias Administrador) permanece protegido no cadastro; grupos subordinados podem ser editados por administradores, inclusive quando possuem flag legado de protecao.
- **Permissoes:** aba Grupos para associar contextos funcionais e restaurar padroes dos grupos de sistema; aba Usuarios para associar grupos, configurar excecoes individuais e restaurar a heranca.
- **Backup e restauracao:** backups imediatos e agendados, com destinos em pasta ou unidade externa; restauracao de ZIPs produzidos pelo Decor.
- **Sobre o Sistema:** versoes do Decor e Avalonia, runtime, sistema operacional e arquitetura do processo. Disponivel em Ajuda para qualquer usuario autenticado.

## Autorizacao

Criacao e edicao de usuarios usam um unico `UserFormViewModel`, com codigo e status imediatamente abaixo do titulo e os mesmos campos e grupos nos dois modos. O nivel hierarquico e interno e nao aparece no formulario. A aba sinaliza criacao/edicao com os indicadores padrao do workspace; a barra de status mostra a acao no singular (por exemplo, `Cadastrando um usuario.` ou `Editando o usuario 10.`), nunca a contagem da listagem enquanto o formulario esta aberto. Produtos, marcas, funcionarios e grupos seguem a mesma redacao.

Na tela Usuarios, consultar grupos usa somente `GetRolesAsync` (`Roles.View`), sem carregar permissoes; salvar exige `Users.AssignRoles` e respeita a hierarquia do servico. Enquanto os grupos carregam ou sao gravados, os checkboxes e a gravacao ficam indisponiveis. Respostas atrasadas de outra selecao sao descartadas. Criacao, edicao e ativacao/desativacao exigem suas respectivas permissoes `Users.*`.

A conta de sistema `admin` e somente leitura para cadastro, estado ativo, grupos e excecoes individuais, inclusive para operadores administradores. Essas alteracoes sao bloqueadas pelo servico; na tela Usuarios, editar, ativar/desativar e salvar grupos ficam desabilitados. O username `admin` e reservado e nao pode ser usado na criacao nem atribuido a outro usuario. A protecao da conta nao se confunde com grupos protegidos: um usuario comum pertencente ao grupo Administradores continua administravel pelas regras existentes. Troca e redefinicao legitimas de senha da conta de sistema permanecem disponiveis.

O cadastro e a consulta de grupos usam `Roles.Edit` e `Roles.View`, respectivamente. O nivel atual e o proposto de um grupo administravel devem ser estritamente menores que o maior nivel do operador; valores maiores representam maior autoridade. Administradores possuem o bypass hierarquico existente, mas nao podem editar o cadastro de Administradores ou usar seu nome reservado. O flag legado de protecao dos subordinados permanece no banco para preservar os contratos de outros servicos; ele nao impede sua edicao no cadastro de grupos.

A tela Permissoes exige `Roles.View`. A aba Usuarios exige adicionalmente `Users.View`. As operacoes continuam protegidas por `Roles.ManagePermissions`, `Roles.RestoreDefaults`, `Users.AssignRoles`, `Users.ManagePermissions` e `Users.RestorePermissions`, conforme a operacao.

As excecoes individuais possuem tres estados: **Herdar dos grupos**, **Permitir** e **Negar**. Herdar remove a excecao; negar impede acesso mesmo quando um grupo concede a permissao. A gravacao preserva as escolhas de todos os modulos, nao apenas do modulo visivel, e preserva excecoes fora do catalogo carregado.

A migracao `20261004_ensure_database_maintenance_permission.sql` e incorporada ao aplicativo e executada pelo bootstrap administrativo antes da carga da sessao. Se `DatabaseMaintenance.View` ainda nao existir, cadastra a permissao e concede o padrao aos grupos protegidos Administradores e Supervisores. Se ja existir, preserva as concessoes e revogacoes atuais. O menu permanece restrito aos usuarios com a permissao efetiva.

Alteracoes de acesso no proprio usuario ou em um grupo da sessao mantem o encerramento de sessao existente. As demais sessoes carregam as novas permissoes no proximo login.

## Restauracao do banco

A consulta exige `DatabaseMaintenance.View`. Restaurar exige adicionalmente `DatabaseMaintenance.Restore`; a migracao `20261004_add_database_restore_permission.sql` concede esse novo padrao somente ao grupo protegido Administradores, na primeira criacao da permissao. Supervisores nao recebem restauracao automaticamente. A permissao pode ser atribuida em Permissoes.

Feche outras sessoes e interrompa operacoes no banco antes de restaurar. Escolha um ZIP confiavel produzido pelo Decor para o mesmo nome de banco configurado, solicite a restauracao e confirme com a frase `RESTAURAR BANCO`. Antes de alterar tabelas, o servico grava uma copia do estado atual na pasta `restore-safety` junto ao ZIP selecionado; se nao conseguir criar essa copia, aborta a operacao.

O servico valida o envelope ZIP/SQL, nome do banco e limites de tamanho (64 MiB compactados e descompactados), remove o preambulo CREATE DATABASE/USE e executa o corpo SQL pelo MySqlConnector. Nao e um sandbox de SQL: somente backups de origem confiavel devem ser utilizados. O conteudo SQL pode executar comandos com os privilegios da conexao. Tabelas ausentes no dump permanecem no banco.

DDL no MariaDB nao possui rollback garantido. Uma falha durante a execucao pode deixar restauracao parcial; a tela preserva o caminho da copia de seguranca e bloqueia novas operacoes. A janela principal fica indisponivel durante a restauracao. Apos sucesso ou falha parcial, um aviso modal apresenta o resultado e encerra a sessao ao fechar; reinicie o Decor antes de continuar. Restauracoes sao serializadas neste processo, mas nao bloqueiam outras instancias do aplicativo ou clientes externos.

O fechamento ao alternar abas de Permissoes era causado pelo identificador de icone voltando ao valor vazio durante a remocao dos estilos. Esse estado agora limpa a geometria em vez de consultar o catalogo.