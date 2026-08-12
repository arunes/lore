namespace Lore.Core.RAG;

public interface IRAGFactory
{
    public IRAGService GetRAGService();
}