# Trabalho 3.1 — Fluxo máximo (Ford-Fulkerson) e busca local

## Executar

Requer SDK do .NET 8 ou superior. Dentro de `M3`:

```sh
dotnet run                                  # menu interativo
dotnet run -- grafos/busca_local.txt        # direto: s = 0, t = V-1
dotnet run -- grafos/classico.txt 0 5       # direto, informando origem e destino
dotnet run -- --test                        # testes
```

A partir da pasta pai: `dotnet run --project M3/TrabalhoM3.Grafos.csproj [-- ...]`.

No menu, escolha o arquivo, a representação (lista ou matriz) e os vértices de origem
e destino (Enter aceita o padrão `0` e `V-1`). O formato do arquivo é o mesmo da M1
(`V A D P` e depois `origem destino capacidade`). O grafo precisa ser direcionado (`D=1`).
Se não for ponderado, cada aresta tem capacidade 1.

## Organização

Mesmo esquema da M2: os fontes ficam em `.src/` (pasta oculta, para a M1 não compilá-los)
e o projeto inclui por referência `../Grafos.cs`, `../GrafoLista.cs` e `../GrafoMatriz.cs`.

- [FluxoMaximo.cs](.src/FluxoMaximo.cs): Ford-Fulkerson, cópia do grafo e busca local;
- [Program.cs](.src/Program.cs): menu e impressão dos resultados;
- [Testes.cs](.src/Testes.cs): testes sem bibliotecas externas.

Única alteração na M1: `Grafos.cs` ganhou a sobrecarga `buscaProfundidade(origem, destino)`.
É a mesma recursão da DFS da M1, que agora também guarda o antecessor de cada vértice,
para quando chega ao destino, devolve o caminho e **ignora arestas de peso 0**.

## Ford-Fulkerson

Segue o pseudocódigo clássico:

```text
f(u,v) = 0 para toda aresta
Gf = cópia de G
enquanto existir caminho p de s a t em Gf (DFS da M1):
    cf(p) = menor capacidade residual em p
    para cada (u,v) em p:
        cf(u,v) -= cf(p)    -> se chegar a 0, a aresta é apagada
        cf(v,u) += cf(p)    -> a aresta reversa é criada se não existir
    fluxo += cf(p)
```

- **Cópia do grafo:** `FluxoMaximo.Copiar` cria uma instância nova (lista ou matriz, como a
  original) e reinsere vértice por vértice e aresta por aresta. Nada é compartilhado por
  referência; o teste confirma que o grafo original sai intacto do Ford-Fulkerson e da busca local.
  O residual é sempre criado como ponderado, porque a base normaliza pesos para 1 em grafos não
  ponderados e isso apagaria as capacidades residuais.
- **Aresta de custo 0:** fica fora da cópia, é apagada do residual quando zera e,
  por garantia, também é ignorada pela DFS.
- **Alterar capacidade:** a base da M1 não tem operação para alterar peso. Por isso a aresta
  é removida e reinserida com o novo valor.
- O fluxo em cada aresta original é `c(u,v) − cf(u,v)`, limitado a `[0, c]`. A saída mostra
  o fluxo máximo, os caminhos aumentantes, o tempo e o fluxo/capacidade de cada aresta
  (quando o grafo tem até 40 arestas).

## Busca local

- **Solução inicial:** a estrutura do grafo lido.
- **Vizinho:** a mesma estrutura com **uma** aresta `u → v` trocada por `v → u` (mesma
  capacidade). Inversões que criariam aresta duplicada (quando `v → u` já existe) e laços
  são ignorados.
- **Avaliação:** o fluxo máximo, calculado pelo Ford-Fulkerson acima.
- **Estratégia:** subida de encosta pelo melhor vizinho. Em cada passo, todos os vizinhos
  são avaliados e o melhor é aceito se aumentar o fluxo. A busca para quando nenhum vizinho
  melhora (ótimo local). Como o fluxo só aumenta, a busca sempre termina.
- **Saída:** o fluxo máximo da solução original e o da final, o **número de passos**
  (inversões aceitas), quantas soluções foram avaliadas, o tempo e cada inversão feita.

Exemplo com `grafos/busca_local.txt` (s = 0, t = 5):

```text
Fluxo máximo da solução original: 4
Fluxo máximo da solução final:    9
Número de passos: 2
Soluções avaliadas (execuções do Ford-Fulkerson): 25
  Passo 1: 2 -> 0 vira 0 -> 2 (capacidade 3); fluxo = 7
  Passo 2: 5 -> 3 vira 3 -> 5 (capacidade 2); fluxo = 9
```

Esse arquivo também mostra o limite da busca local: o caminho `0 → 4 → 5`, que daria
fluxo 10, exige inverter duas arestas ao mesmo tempo (`4 → 0` e `5 → 4`). Uma inversão
sozinha não aumenta o fluxo, então a busca para no ótimo local 9.
Em `grafos/classico.txt` o fluxo vai de 23 para 24 em um passo (invertendo `3 → 2`).

Custo: cada passo roda o Ford-Fulkerson uma vez por aresta, além de copiar o grafo.
Em grafos grandes, prefira a representação em lista.

## Testes (`--test`)

- Fluxos conhecidos (exemplo clássico = 23, destino inalcançável, antiparalelas, laço,
  capacidades fracionárias) em lista e matriz, verificando capacidade e conservação.
- 300 grafos aleatórios comparados com o **corte mínimo** obtido por enumeração.
- Grafo original intacto, aresta de capacidade 0 ignorada, caminho da DFS e grafo não ponderado.
- Busca local: resultado esperado do exemplo, melhora estrita a cada passo, inversões que
  reproduzem a solução final, número de arestas preservado e confirmação de que a solução
  final é um ótimo local (nenhum vizinho tem fluxo maior). Também testa o limite de passos.
- Leitura dos arquivos de `grafos/`.
