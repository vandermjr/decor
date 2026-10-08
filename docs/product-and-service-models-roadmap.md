# Modelos de produtos e servicos: proposta futura

Status: ideia aprovada para amadurecimento futuro; nao implementar nesta etapa.

## Cadastros distintos

- Produtos representam mercadorias, materiais, acessorios e produtos acabados.
- Servicos possuem cadastro e tabela proprios, separados de produtos.
- Modelos de Produto representam configuracoes reutilizaveis para resultados
  manufaturados, como cortina manual, trilho simples, trilho duplo e motorizada.
- Modelos de Servico representam configuracoes reutilizaveis de atividades,
  podendo incluir materiais consumidos.
- Kit fica reservado para conjuntos comerciais de composicao definida. Um
  modelo nao deve ser cadastrado como produto apenas para reutilizar sua receita.

## Uso em orcamentos e pedidos

Selecionar um modelo cria uma copia independente de sua configuracao no
orcamento. Medidas, componentes, servicos, quantidades e valores podem ser
adaptados naquela copia, sem alterar o modelo original. Atualizacoes posteriores
do modelo nao modificam documentos existentes.

Uma cortina personalizada pode ser um item manufaturado daquele pedido, com
composicao e eventual ordem de fabricacao, sem gerar um cadastro de produto para
cada combinacao de cliente, medidas e materiais. Uma cortina pronta e estocada
pode ter cadastro proprio e ficha tecnica.

## Decisoes ainda pendentes

- Apresentacao comercial: item completo, componentes discriminados, ou ambas.
- Parametros e formulas de consumo por largura, altura, acabamento e perdas.
- Diferenciacao entre personalizacao esperada e alteracao livre de composicao.
- Versionamento, rastreabilidade, precificacao e vinculo com fabricacao.
- Tratamento de materiais em modelos de servico e prevencao de ciclos.

A separacao atual entre produtos e servicos nao deve antecipar a implementacao
destes modelos nem remover estruturas de kit existentes sem migracao especifica.