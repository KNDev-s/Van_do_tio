# Van do Tio — piloto de 30 dias

Repositório privado do aplicativo de transporte escolar da KNDev's.

## Status

**Inicialização realizada. Código do piloto V0.1 ainda não importado.** O pacote de 29 arquivos está disponível no chat onde foi produzido como `Van_Escolar_Piloto_V0_1.zip`.

Não liberar acesso externo com dados reais de crianças antes de compilar a API, aplicar migrations de forma controlada, validar autenticação, autorização entre contas, pagamentos e backups.

## Estrutura prevista

```
apps/
  api/  # ASP.NET Core 10, EF Core, PostgreSQL, Identity
  web/  # React 19, Vite, TypeScript (Vercel)
docs/
tests/
.github/workflows/
compose.yaml
```

## Importar o pacote

Extraia `Van_Escolar_Piloto_V0_1.zip`. Copie **o conteúdo interno** da pasta `van-escolar-piloto` para a raiz deste repositório, preservando diretórios e dotfiles. Não envie o ZIP compactado como substituto do código.

O frontend deve ter **Root Directory = `apps/web`** na Vercel. A API .NET é hospedada separadamente, com HTTPS e PostgreSQL persistente.

## Próximos passos

1. Importar código V0.1.
2. Executar npm install/npm run build e dotnet restore/dotnet build.
3. Criar e revisar migration do EF Core, testar PostgreSQL.
4. Configurar infraestrutura, cookies, CSRF e isolamento de tenants.
5. Executar smoke/E2E e autorizar o piloto somente após a homologação.

