# Van Escolar — Plano de validação com cliente (30 dias)

## Objetivo
Descobrir se um transportador consegue administrar alunos, contratos, mensalidades, recebimentos e despesas no uso real, sem treinamento técnico ou planilhas paralelas.

## Pré-requisitos para convidar o cliente
- [ ] API compilada, migrations revisadas, builds e CI verdes.
- [ ] Login, logout, CSRF, autorização por tenant e isolamento validados com testes.
- [ ] Banco persistente provisionado, backups automáticos e restauração testada.
- [ ] Termos de tratamento de dados e política de retenção ajustados para dados pessoais de menores.
- [ ] Recuperação de acesso e suporte humano definidos.
- [ ] Conta proprietária criada e senha entregue de maneira segura.
- [ ] Fluxos essenciais testados no smartphone real do cliente.

## Semana 1 — Cadastro e familiarização
Validar onboarding, escolas, alunos, responsáveis, endereços e contratos. Perguntar quais campos foram difíceis.

## Semana 2 — Controle de cobranças
Validar mensalidades diferentes, vencimentos individuais e registro de recebimentos. Capturar tempo para registrar um pagamento e erros encontrados.

## Semana 3 — Rotina diária
Validar dashboard, ausências e despesas. Comparar o controle registrado com a operação real, sem coletar dados pessoais no documento de feedback.

## Semana 4 — Avaliação e priorização
Revisar falhas, dúvidas e recorrência de uso. Identificar principais tarefas pendentes e decidir o próximo desenvolvimento.

## Métricas sugeridas (metas a validar, não dados observados)
- 100% dos alunos do piloto cadastrados.
- 90% ou mais das mensalidades do mês acompanhadas pelo app.
- Tempo mediano para registrar pagamento inferior a 30 segundos.
- Nenhuma perda de dados, duplicidade indevida ou acesso não autorizado.
- Feedback semanal do cliente, sem dados de identificação dos menores.

## Perguntas de feedback
1. O que você ainda faz no caderno/WhatsApp que deveria estar no app?
2. Qual tarefa hoje demora mais?
3. Você consegue identificar rapidamente quem está devendo?
4. Qual etapa foi confusa?
5. O que falta para usar apenas este sistema?

## Pontos de risco conhecidos da V0.1
- Sem edição/cancelamento de contratos ou alunos; correções exigem evolução antes de dados reais.
- Sem estorno/recibos e recuperação de senha.
- A geração de mensalidades é manual, não automatizada.
- Ausências são registradas pelo próprio operador (portal do responsável não entregue).
- O usuário owner é provisionado manualmente pela administração.

## Critério de saída do piloto
Produto só avança à abertura para múltiplos clientes após correção dos bloqueadores e auditoria de segurança, revisão jurídica do tratamento de dados, métricas de uso positivas e testes completos de backup/restore.
