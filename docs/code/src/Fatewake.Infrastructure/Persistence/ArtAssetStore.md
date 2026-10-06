# ArtAssetStore

Filters and orders canonical asset records before projecting storage keys with correlated derivative subqueries. Keeping predicates on entity members, rather than on an intermediate constructed record, preserves EF translation to PostgreSQL. Reads are untracked.
