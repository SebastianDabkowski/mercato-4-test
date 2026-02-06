using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Cart.Application
{
    public class RemoveFromCart
    {
        private readonly ICartRepository _repository;

        public RemoveFromCart(ICartRepository repository)
        {
            _repository = repository;
        }

        public async Task ExecuteAsync(int cartItemId)
        {
            await _repository.RemoveAsync(cartItemId);
        }
    }
}
