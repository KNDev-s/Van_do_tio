# Van Escolar — Piloto de 30 dias (V0.1)

**Projeto-fonte inicial para um cliente piloto**, baseado na especificação "Gestão de Transporte Escolar V1 — Pack IA". Sem dados fictícios persistidos e sem `localStorage` para dados financeiros.

> ⚠️ Estado da entrega: **scaffold funcional proposto, ainda não homologado**. É necessário executar instalação de dependências, gerar/aplicar migrations, build, testes e revisão de segurança antes de permitir dados pessoais de crianças no ambiente público. Este pacote foi criado em ambiente SEM .NET SDK e SEM acesso ao registro NPM; não foi possível executar builds ou E2E aqui. O GitHub Actions compilador deve ajudar a identificar ajustes no primeiro push.

## Arquitetura de hospedagem

```
Usuário -> HTTPS Vercel (React/Vite estático)
               |
               | /api/v1/*  [rewrite externo]
               v
          HTTPS API ASP.NET Core 10 (hosting Docker, separado da Vercel)
               |
               v
         PostgreSQL persistente (backup diário)
```

**Vercel NÃO hospeda este backend ASP.NET Core em um runtime oficial**. Não substituir backend por armazenamento local ou soluções inseguras. A Vercel encaminha `/api` à API por `rewrites` mantendo o domínio da interface para os cookies HttpOnly e cabeçalho CSRF.

## O que foi codificado neste piloto

- Login de um proprietário pré-provisionado por linha de comando (sem cadastro público).
- Cookie seguro com ASP.NET Core Identity, lockout e CSRF.
- Isolamento de dados por `TenantId` obtido do vínculo do usuário autenticado no servidor.
- Escolas: listar/cadastrar.
- Alunos: listar/cadastrar, com criação de responsável e vínculo.
- Contratos mensais: cadastrar, vencimento individual 1–28 e até 36 parcelas.
- Gerar cobranças de uma competência por ação manual, sem duplicar contrato/mês.
- Mensalidades: listar, indicadores e registro de pagamento parcial ou integral, com chave de idempotência e lock transacional.
- Despesas: cadastrar/listar.
- Ausências: registrar/listar/confirmar ciência pelo operador.
- Dashboard com valores reais, indicadores de caixa e faltas do dia.
- Layout mobile-first e navegação responsiva.
- Audit trail básico das mutações.

## Fora deste V0.1 (ainda não implemente ou publique como se existisse)

- Cadastro público, confirmação por e-mail, recuperação de senha, múltiplos operadores e portal do responsável.
- Alteração/inativação de cadastros; cancelamento/edição de contratos; pagamento anual; multas; estorno; recibos PDF.
- Jobs agendados Quartz, cobrança automática, notificações WhatsApp/e-mail.
- Audit trail completo, backup automatizado, monitoramento e testes E2E.
- Rate limit distribuído por Redis (o rate limit atual é por instância).
- MFA, uploads seguros, operações financeiras avançadas, assinatura contratual e relatórios exportáveis.

**Para usar comercialmente com dados reais, é necessário completar pelo menos os controles de autenticação, recuperação de acesso, testes de autorização/concorrência e política de retenção/backup.** Não coloque o produto para um cliente armazenar dados de crianças sem essa validação.

## Desenvolvimento local — passo a passo

Pré-requisitos: Node 22+, npm, .NET SDK 10, Docker Desktop, Git.

1. Copie `.env.example` para `.env`, altere a senha local e execute `docker compose up -d db` na raiz.
2. Em um terminal na raiz, configure a conexão da API e instale a ferramenta EF (exemplo **PowerShell**):

```powershell
$env:ConnectionStrings__MainDb='Host=localhost;Port=5432;Database=vanescolar;Username=vanescolar;Password=SENHA_DO_ENV_LOCAL'
dotnet tool restore
# Uma única vez, criando a migration real a partir do modelo EF:
dotnet ef migrations add InitialPilot --project apps/api --output-dir Data/Migrations
# Execute as migrations somente depois de revisar o SQL gerado:
dotnet ef migrations script --project apps/api --output apps/api/Data/initial.sql
# Inspecione o arquivo apps/api/Data/initial.sql antes de prosseguir.
dotnet ef database update --project apps/api
```

3. Provisione o primeiro proprietário (sem senha padrão em código):

```powershell
$env:BOOTSTRAP_ADMIN_EMAIL='operador@exemplo.com'
$env:BOOTSTRAP_ADMIN_PASSWORD='DEFINA_UMA_SENHA_FORTE_AQUI_2026!'
$env:BOOTSTRAP_OPERATION_NAME='Transporte do Cliente'
dotnet run --project apps/api -- --bootstrap-owner
Remove-Item Env:\BOOTSTRAP_ADMIN_EMAIL
Remove-Item Env:\BOOTSTRAP_ADMIN_PASSWORD
Remove-Item Env:\BOOTSTRAP_OPERATION_NAME
```

4. Inicie a API em HTTP **somente no computador local**:

```powershell
dotnet run --project apps/api --urls http://localhost:5182
```

5. Em outro terminal:

```powershell
cd apps/web
npm install
npm run dev
```

6. Abra `http://localhost:5173`. O Vite encaminha `/api` para `http://localhost:5182`. Entre com o e-mail e a senha configurados acima.

### Rotina básica para testar

1. Crie uma escola.
2. Cadastre um aluno, endereços e responsável.
3. Na página Alunos, crie um contrato para esse aluno.
4. Em Mensalidades, escolha o mês e clique em **Gerar mensalidades**.
5. Registre um pagamento; verifique o dashboard.
6. Adicione despesas e ausências; confirme ciência do aviso.
7. Atualize a página e faça logout/login para validar persistência no PostgreSQL.

## Como subir na Vercel, após validar o backend

1. Suba os arquivos deste ZIP no repositório GitHub que você está criando.
2. **Backend**: coloque `apps/api` em uma hospedagem de containers ASP.NET Core 10 (ex.: Render, VPS, Fly.io ou outro serviço que suporte Docker), com um banco PostgreSQL persistente separado e backups. Use o Dockerfile em `apps/api/Dockerfile` e configure `ConnectionStrings__MainDb` diretamente no painel de secrets. Exponha HTTPS público no serviço da API, mesmo que seja um domínio provisório.
3. **Migrations de produção**: gere e comite a migration que foi testada localmente. Execute `dotnet ef database update` em uma etapa de migração controlada, com backup, **não no startup do servidor**.
4. Execute o bootstrap do proprietário **uma única vez** em console/job administrativo da hospedagem e remova as variáveis de bootstrap.
5. Edite `apps/web/vercel.json`: substitua `https://SUBSTITUIR-PELA-API-HTTPS.example.com` pelo domínio HTTPS real da sua API. **Não aponte para localhost.** Não habilite cache público de `/api`.
6. Vercel > Add New > Project > importe o mesmo repositório.
7. Em **Root Directory**, informe `apps/web`.
8. Framework Preset: **Vite**; Install: `npm install`; Build: `npm run build`; Output: `dist`.
9. Publique uma preview; teste login, cookies, recarga, POST com antiforgery, pagamentos sem duplicidade, proteção entre tenants e ausência de vazamento de dados. Só depois publique para o cliente.

**A Vercel hospeda somente a UI.** Se publicar sem uma API conectada, o app mostrará aviso de indisponibilidade e não poderá autenticar nem salvar dados.

**Licenciamento:** Vercel Hobby tem restrição de uso pessoal e não comercial; para o piloto de um cliente verifique plano adequado, geralmente Pro.

## Segurança e operação por 30 dias

- Hospede o banco fora de serviços efêmeros; não use volume temporário para dados reais.
- Ative backups automáticos DIÁRIOS, teste restauração antes do piloto e restrinja acesso ao PostgreSQL.
- Habilite HTTPS, domínio estável, variáveis de ambiente protegidas e rotação de senhas.
- Não encaminhe links de preview públicos contendo dados do cliente para terceiros.
- Valide sessão, CSRF, lockout e isolamento cross-tenant antes do uso externo.
- Na integração Vercel → API, verifique que cookies são criados para o domínio que o cliente vê, e que nenhum cabeçalho de cache público é aplicado às respostas autenticadas.
- Registre feedback semanalmente sem copiar nomes, endereços e dados pessoais de crianças para ferramentas de analytics.
- O proprietário do transporte deve informar finalidade dos dados coletados e limitar acesso conforme LGPD; valide documentos/consentimentos aplicáveis com especialista.

## Links técnicos

- https://vercel.com/docs/frameworks/frontend/vite
- https://vercel.com/docs/routing/rewrites
- https://learn.microsoft.com/aspnet/core/security/authentication/identity
- https://www.npgsql.org/efcore/

## Documentação complementar

Leia `docs/PLANO_PILOTO.md` para objetivos e coleta de feedback. A especificação original completa continua no Google Drive: **Transporte Escolar V1 — Pack IA**.
