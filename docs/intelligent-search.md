# Shared intelligent search

`Decor.Core.Common.IntelligentSearchQuery` owns whitespace tokenization,
double-quoted phrases and numeric ID interpretation. It preserves case, accents
and token order. Empty input adds no search predicates. Empty quotes are ignored;
an unclosed quote makes the rest of the input one phrase. Phrase boundaries are
trimmed, but internal whitespace is preserved. Quotes cannot be escaped.

`WithDynamicSearchFilter` requires every token (AND). Each token may match any
of the supplied text columns (OR). It uses parameterized LIKE expressions and
groups the complete search so surrounding conditions cannot change its meaning.
Existing one-column callers need no changes. The returned chain still supports
conditions, ID-search metadata and ordering.

A single Int32 value uses exact ID equality, including a leading sign, zero and
negative values. Leading zeros are accepted. Overflow falls back to a text token,
matching the previous generic filter behavior. Numeric tokens in a multi-token
generic query are text, not independent ID conditions.

`ProductSearchQuery` retains product type and stock grammar, bound merging and
conflict validation. Filters are interpreted only outside double quotes. Product
codes remain digits-only: overflow still selects the impossible ID zero rather
than falling back to text. The product repository retains OR across description,
brand, barcode, manufacturer reference and an integer ID for mixed queries.

User administration now uses the shared filter for Username and DisplayName,
including exact UserID searches. Roles are returned by GetRolesAsync without a
search argument, so no role-query or authorization behavior changes.

This is tokenized SQL substring search, not fuzzy matching, stemming or ranking.
Case and accent matching depend on database collation. LIKE wildcard characters
`%` and `_` retain their existing behavior. Frontend highlighting, input controls,
viewmodels and authorization rules are outside this change.