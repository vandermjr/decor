# SqlBuilder

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![GitHub Actions Workflow Status](https://img.shields.io/badge/build-passing-green)
![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)

Uma biblioteca .NET para construir consultas SQL dinâmicas e seguras de forma fluente e tipada. Com uma abordagem orientada a objetos, ela abstrai a construção de strings SQL, minimizando erros de sintaxe e protegendo contra injeção de SQL por meio de parâmetros parametrizados.

## 🚀 Recursos Principais

-   **Fluent API**: Crie queries SQL complexas de forma legível e encadeada.
-   **Segurança**: Todas as queries são construídas usando parâmetros, prevenindo automaticamente ataques de injeção de SQL.
-   **Tipagem Forte**: Utilize expressões `lambda` para referenciar propriedades de entidades, garantindo que as consultas sejam refatoráveis e seguras em tempo de compilação.
-   **Multi-Dialeto**: Projetada com uma arquitetura flexível para suportar diferentes dialetos SQL (MariaDB por padrão), permitindo fácil extensão para outros bancos de dados como SQL Server, PostgreSQL, etc.
-   **Operações CRUD**: Suporta a construção de queries `SELECT`, `INSERT`, `UPDATE` e `DELETE`.
-   **Funcionalidades Avançadas**: Inclui suporte a `JOIN`, cláusulas `WHERE` com `AND`/`OR`, `LIKE`, `COUNT`, e paginação (`LIMIT`/`OFFSET`).

## 📦 Instalação

Adicione o pacote `SqlBuilder` ao seu projeto usando o NuGet Package Manager.

```bash
dotnet add package SqlBuilder


📖 Como Usar
A classe QueryBuilder é o ponto de entrada principal para a construção de todas as consultas.

1. Configuração Inicial
Primeiro, registre suas entidades e seus respectivos aliases.

// Exemplo de entidades
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Address
{
    public int Id { get; set; }
    public string Street { get; set; }
    public int UserId { get; set; }
}

// Inicia o construtor de queries
var queryBuilder = QueryBuilder.Query()
    .RegisterAlias<User>("u")
    .RegisterAlias<Address>("a");
	
2. SELECT com JOIN e WHERE
Este exemplo demonstra como construir uma consulta SELECT com JOIN, WHERE, ORDER BY e paginação.

var (sql, parameters) = queryBuilder
    .Select<User>(u => new { u.Id, u.Name }) // Seleciona colunas específicas da entidade User
    .AllColumns<Address>() // Seleciona todas as colunas da entidade Address
    .From<User>()
    .InnerJoin<User, Address>((u, a) => u.Id == a.UserId)
    .Where<User>(u => u.Name).Like.Contains("joão")
    .And<Address>(a => a.Street).Equals("Rua Principal")
    .And<Address>(a => a.Id).IsNotNull()
    .OrderByDescending<User>(u => u.Id)
    .Skip(10).Take(20)
    .Build();

// SQL gerado:
// SELECT u.Id, u.Name, a.*
// FROM users AS u
// INNER JOIN addresses AS a ON u.Id = a.UserId
// WHERE u.Name LIKE CONCAT('%', @Name) AND a.Street = @Street AND a.Id IS NOT NULL
// ORDER BY u.Id DESC
// LIMIT 20 OFFSET 10;

3. INSERT
var newUser = new User { Name = "Maria" };
var (sql, parameters) = queryBuilder
    .InsertInto<User>()
    .Values(newUser)
    .Build();

// SQL gerado:
// INSERT INTO users (Name)
// VALUES (@Name);

4. UPDATE
var updatedUser = new User { Name = "José da Silva" };
var (sql, parameters) = queryBuilder
    .Update<User>()
    .Set(u => u.Name, updatedUser.Name)
    .Where<User>(u => u.Id).Equals(10)
    .Build();

// SQL gerado:
// UPDATE users AS u
// SET Name = @Name
// WHERE u.Id = @Id;

5. DELETE
var (sql, parameters) = queryBuilder
    .Delete<User>()
    .Where<User>(u => u.Id).Equals(10)
    .Build();

// SQL gerado:
// DELETE FROM users AS u
// WHERE u.Id = @Id;

6. Contagem de Registros
Use o método Count para obter o número total de registros.

// Exemplo de COUNT(1)
var (sql, parameters) = queryBuilder
    .Select<User>().Count(1)
    .From<User>()
    .Where<User>(u => u.Name).Like.Contains("Maria")
    .Build();

// SQL gerado:
// SELECT COUNT(1)
// FROM users AS u
// WHERE u.Name LIKE CONCAT('%', @Name);

7. Cláusula LIKE
A cláusula Like oferece métodos fluentes para diferentes tipos de busca.

// LIKE '%termo%'
queryBuilder.Where<User>(u => u.Name).Like.Contains("Maria");

// LIKE 'termo%'
queryBuilder.Where<User>(u => u.Name).Like.StartsWith("Maria");

// LIKE '%termo'
queryBuilder.Where<User>(u => u.Name).Like.EndsWith("Maria");

🤝 Contribuição
Contribuições são bem-vindas! Siga os passos abaixo:

Faça um fork do repositório.

Crie uma nova branch (git checkout -b feature/nome-da-feature).

Faça suas alterações e commit (git commit -am 'Adiciona nova feature').

Envie para a branch (git push origin feature/nome-da-feature).

Abra um Pull Request.

📄 Licença
Este projeto está licenciado sob a Licença MIT.