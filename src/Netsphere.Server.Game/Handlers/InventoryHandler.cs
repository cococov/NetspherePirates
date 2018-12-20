using System.Threading.Tasks;
using Logging;
using Netsphere.Network;
using Netsphere.Network.Message.Game;
using ProudNet;

namespace Netsphere.Server.Game.Handlers
{
    internal class InventoryHandler
        : IHandle<CUseItemReqMessage>, IHandle<CRepairItemReqMessage>, IHandle<CRefundItemReqMessage>,
          IHandle<CDiscardItemReqMessage>
    {
        private readonly ILogger _logger;

        public InventoryHandler(ILogger<InventoryHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> OnHandle(MessageContext context, CUseItemReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var character = plr.CharacterManager[message.CharacterSlot];
            var item = plr.Inventory[message.ItemId];

            if (character == null || item == null || plr.Room != null && plr.State != PlayerState.Lobby)
            {
                await session.SendAsync(new SServerResultInfoAckMessage(ServerResult.FailedToRequestTask));
                return true;
            }

            switch (message.Action)
            {
                case UseItemAction.Equip:
                    character.Equip(item, message.EquipSlot);
                    break;

                case UseItemAction.UnEquip:
                    character.UnEquip(item.ItemNumber.Category, message.EquipSlot);
                    break;
            }

            return true;
        }

        public async Task<bool> OnHandle(MessageContext context, CRepairItemReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var logger = plr.AddContextToLogger(_logger);

            foreach (var id in message.Items)
            {
                var item = session.Player.Inventory[id];
                if (item == null)
                {
                    logger.Warning("Item={ItemId} not found", id);
                    await session.SendAsync(new SRepairItemAckMessage(ItemRepairResult.Error0, 0));
                    return true;
                }

                if (item.Durability == -1)
                {
                    logger.Warning("Item={ItemId} can not be repaired", id);
                    await session.SendAsync(new SRepairItemAckMessage(ItemRepairResult.Error1, 0));
                    return true;
                }

                var cost = item.CalculateRepair();
                if (plr.PEN < cost)
                {
                    await session.SendAsync(new SRepairItemAckMessage(ItemRepairResult.NotEnoughMoney, 0));
                    return true;
                }

                var price = item.GetShopPrice();
                if (price == null)
                {
                    logger.Warning("No shop entry found item={ItemId}", id);
                    await session.SendAsync(new SRepairItemAckMessage(ItemRepairResult.Error2, 0));
                    return true;
                }

                if (item.Durability >= price.Durability)
                {
                    await session.SendAsync(new SRepairItemAckMessage(ItemRepairResult.OK, item.Id));
                    continue;
                }

                item.Durability = price.Durability;
                plr.PEN -= cost;

                await session.SendAsync(new SRepairItemAckMessage(ItemRepairResult.OK, item.Id));
                await plr.SendMoneyUpdate();
            }

            return true;
        }

        public async Task<bool> OnHandle(MessageContext context, CRefundItemReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var item = plr.Inventory[message.ItemId];
            var logger = plr.AddContextToLogger(_logger);

            if (item == null)
            {
                logger.Warning("Item={ItemId} not found", message.ItemId);
                await session.SendAsync(new SRefundItemAckMessage(ItemRefundResult.Failed, 0));
                return true;
            }

            var price = item.GetShopPrice();
            if (price == null)
            {
                logger.Warning("No shop entry found item={ItemId}", message.ItemId);
                await session.SendAsync(new SRefundItemAckMessage(ItemRefundResult.Failed, 0));
                return true;
            }

            if (!price.CanRefund)
            {
                logger.Warning("Cannot refund item={ItemId}", message.ItemId);
                await session.SendAsync(new SRefundItemAckMessage(ItemRefundResult.Failed, 0));
                return true;
            }

            plr.PEN += item.CalculateRefund();
            plr.Inventory.Remove(item);

            await session.SendAsync(new SRefundItemAckMessage(ItemRefundResult.OK, item.Id));
            await plr.SendMoneyUpdate();

            return true;
        }

        public async Task<bool> OnHandle(MessageContext context, CDiscardItemReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var item = plr.Inventory[message.ItemId];
            var logger = plr.AddContextToLogger(_logger);

            if (item == null)
            {
                logger.Warning("Item={ItemId} not found", message.ItemId);
                await session.SendAsync(new SDiscardItemAckMessage(2, 0));
                return true;
            }

            var shopItem = item.GetShopItem();
            if (shopItem == null)
            {
                logger.Warning("No shop entry found item={ItemId}", message.ItemId);
                await session.SendAsync(new SDiscardItemAckMessage(2, 0));
                return true;
            }

            if (!shopItem.IsDestroyable)
            {
                logger.Warning("Cannot discard item={ItemId}", message.ItemId);
                await session.SendAsync(new SDiscardItemAckMessage(2, 0));
                return true;
            }

            plr.Inventory.Remove(item);
            await session.SendAsync(new SDiscardItemAckMessage(0, item.Id));

            return true;
        }
    }
}
