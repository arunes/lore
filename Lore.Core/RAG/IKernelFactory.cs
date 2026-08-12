using Microsoft.SemanticKernel;

namespace Lore.Core.RAG;

public interface IKernelFactory
{
    Kernel CreateKernel();
}