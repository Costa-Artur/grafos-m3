#nullable enable
using System.Diagnostics;
using TrabalhoM1.Grafos;

namespace TrabalhoM3;

/// <summary>Fluxo atribuído a uma aresta do grafo original.</summary>
public readonly record struct ArestaFluxo(int Origem, int Destino, float Capacidade, float Fluxo);

public sealed record ResultadoFluxo(double FluxoMaximo, IReadOnlyList<ArestaFluxo> Arestas,
    int CaminhosAumentantes, TimeSpan Duracao);

/// <summary>A aresta Origem -> Destino da solução corrente passou a ser Destino -> Origem.</summary>
public readonly record struct Inversao(int Origem, int Destino, float Capacidade, double FluxoApos);

public sealed record ResultadoBuscaLocal(double FluxoOriginal, double FluxoFinal, int Passos,
    int SolucoesAvaliadas, IReadOnlyList<Inversao> Inversoes, Grafos SolucaoFinal, TimeSpan Duracao);

public static class FluxoMaximo
{
    // Tolerância relativa para considerar que um vizinho é realmente melhor.
    // Evita aceitar "melhorias" que são só ruído de arredondamento de float.
    private const double ToleranciaRelativa = 1e-6;

    /// <summary>
    /// Cópia profunda: cria uma instância nova (mesma representação) e reinsere
    /// vértices e arestas um a um. Nenhuma estrutura interna é compartilhada com
    /// o original, então alterar a cópia nunca altera o grafo recebido.
    /// </summary>
    public static Grafos Copiar(Grafos grafo, bool? ponderado = null, bool ignorarPesoZero = false)
    {
        bool p = ponderado ?? grafo.Ponderado;
        Grafos copia = grafo is GrafoMatriz
            ? new GrafoMatriz(grafo.Direcionado, p)
            : new GrafoLista(grafo.Direcionado, p);
        for (int v = 0; v < grafo.QuantidadeVertices; v++)
            copia.inserirVertice(grafo.labelVertice(v));
        for (int v = 0; v < grafo.QuantidadeVertices; v++)
            foreach (int u in grafo.retornarVizinhos(v))
            {
                float peso = grafo.pesoAresta(v, u);
                if (ignorarPesoZero && peso <= 0) continue;
                copia.inserirAresta(v, u, peso);
            }
        return copia;
    }

    private static void Validar(Grafos grafo, int origem, int destino)
    {
        if (!grafo.Direcionado)
            throw new ArgumentException("Fluxo máximo exige grafo direcionado.");
        int n = grafo.QuantidadeVertices;
        if (origem < 0 || origem >= n || destino < 0 || destino >= n)
            throw new ArgumentException($"Origem e destino devem estar entre 0 e {n - 1}.");
        if (origem == destino)
            throw new ArgumentException("Origem e destino devem ser vértices diferentes.");
        for (int v = 0; v < n; v++)
            foreach (int u in grafo.retornarVizinhos(v))
                if (grafo.pesoAresta(v, u) < 0)
                    throw new ArgumentException($"Capacidade negativa na aresta {v} -> {u}.");
    }

    /// <summary>
    /// Ford-Fulkerson:
    ///   f(u,v) = 0 para toda aresta
    ///   Gf = cópia de G (grafo residual)
    ///   enquanto existir caminho p de s a t em Gf (DFS):
    ///       cf(p) = min { cf(u,v) : (u,v) em p }
    ///       para cada (u,v) em p:
    ///           cf(u,v) -= cf(p)      (aresta que zera é apagada)
    ///           cf(v,u) += cf(p)      (aresta reversa, criada se não existir)
    ///       fluxo += cf(p)
    /// O grafo recebido não é alterado.
    /// </summary>
    public static ResultadoFluxo FordFulkerson(Grafos grafo, int origem, int destino)
    {
        var relogio = Stopwatch.StartNew();
        Validar(grafo, origem, destino);

        // O residual é sempre ponderado: em um grafo não ponderado a base normaliza
        // todo peso para 1, o que impediria guardar capacidades residuais.
        // Arestas de capacidade 0 já ficam fora da cópia.
        Grafos residual = Copiar(grafo, ponderado: true, ignorarPesoZero: true);

        double fluxoTotal = 0;
        int caminhos = 0;
        while (true)
        {
            List<int> caminho = residual.buscaProfundidade(origem, destino);
            if (caminho.Count == 0) break;

            float gargalo = float.MaxValue;
            for (int i = 0; i + 1 < caminho.Count; i++)
                gargalo = Math.Min(gargalo, residual.pesoAresta(caminho[i], caminho[i + 1]));

            for (int i = 0; i + 1 < caminho.Count; i++)
            {
                AjustarCapacidade(residual, caminho[i], caminho[i + 1], -gargalo);
                AjustarCapacidade(residual, caminho[i + 1], caminho[i], gargalo);
            }

            fluxoTotal += gargalo;
            caminhos++;
        }

        // Fluxo em cada aresta original: f(u,v) = c(u,v) - cf(u,v), limitado a [0, c].
        // Com arestas antiparalelas o residual combina as duas; o resultado é o fluxo líquido.
        var arestas = new List<ArestaFluxo>();
        for (int v = 0; v < grafo.QuantidadeVertices; v++)
            foreach (int u in grafo.retornarVizinhos(v))
            {
                float capacidade = grafo.pesoAresta(v, u);
                float fluxo = Math.Clamp(capacidade - residual.pesoAresta(v, u), 0f, Math.Max(capacidade, 0f));
                arestas.Add(new(v, u, capacidade, fluxo));
            }

        relogio.Stop();
        return new(fluxoTotal, arestas.AsReadOnly(), caminhos, relogio.Elapsed);
    }

    /// <summary>
    /// A base da M1 não tem "alterar peso": a aresta é removida e reinserida.
    /// Se a nova capacidade for 0, a aresta simplesmente não volta (é apagada).
    /// </summary>
    private static void AjustarCapacidade(Grafos residual, int u, int v, float delta)
    {
        float nova = residual.pesoAresta(u, v) + delta; // pesoAresta devolve 0 se não existir
        residual.removerAresta(u, v);
        if (nova > 0) residual.inserirAresta(u, v, nova);
    }

    /// <summary>
    /// Busca local (subida de encosta, melhor vizinho):
    ///   solução inicial = estrutura do grafo recebido;
    ///   vizinhos = mesma estrutura com UMA aresta u -> v trocada por v -> u (mesma capacidade);
    ///   avaliação = fluxo máximo por Ford-Fulkerson.
    /// A cada passo todos os vizinhos são avaliados e o melhor é aceito se aumentar o fluxo.
    /// Para quando nenhum vizinho melhora (ótimo local) ou ao atingir maxPassos.
    /// Inversões que criariam aresta paralela (v -> u já existe) e laços são ignorados.
    /// </summary>
    public static ResultadoBuscaLocal BuscaLocal(Grafos grafo, int origem, int destino, int maxPassos = int.MaxValue)
    {
        var relogio = Stopwatch.StartNew();
        Validar(grafo, origem, destino);

        Grafos atual = Copiar(grafo, ponderado: true);
        double fluxoAtual = FordFulkerson(atual, origem, destino).FluxoMaximo;
        double fluxoOriginal = fluxoAtual;
        int avaliadas = 1, passos = 0;
        var inversoes = new List<Inversao>();

        while (passos < maxPassos)
        {
            Grafos? melhor = null;
            double melhorFluxo = fluxoAtual;
            Inversao melhorInversao = default;

            for (int u = 0; u < atual.QuantidadeVertices; u++)
                foreach (int v in atual.retornarVizinhos(u))
                {
                    if (u == v || atual.existeAresta(v, u)) continue;

                    float capacidade = atual.pesoAresta(u, v);
                    Grafos vizinha = Copiar(atual);
                    vizinha.removerAresta(u, v);
                    vizinha.inserirAresta(v, u, capacidade);

                    double fluxo = FordFulkerson(vizinha, origem, destino).FluxoMaximo;
                    avaliadas++;
                    if (fluxo > melhorFluxo + ToleranciaRelativa * Math.Max(1, Math.Abs(melhorFluxo)))
                    {
                        melhor = vizinha;
                        melhorFluxo = fluxo;
                        melhorInversao = new(u, v, capacidade, fluxo);
                    }
                }

            if (melhor is null) break; // ótimo local
            atual = melhor;
            fluxoAtual = melhorFluxo;
            inversoes.Add(melhorInversao);
            passos++;
        }

        relogio.Stop();
        return new(fluxoOriginal, fluxoAtual, passos, avaliadas, inversoes.AsReadOnly(), atual, relogio.Elapsed);
    }
}
