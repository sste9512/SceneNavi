using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyNamespace;

namespace SceneNavi.Services.Commands
{
    public class MessageBoxQueryHandler : IRequestHandler<MessageBoxRequest>
    {
        private readonly ClientPersistenceService _clientPersistenceService;

        public MessageBoxQueryHandler(ClientPersistenceService clientPersistenceService)
        {
            _clientPersistenceService = clientPersistenceService;
        }
        
        public Task<Unit> Handle(MessageBoxRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}