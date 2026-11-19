use super::{RemoteTextDisplay, Shows};

impl RemoteTextDisplay {
    pub(in crate::game) fn hz_jade_block(&self) -> Option<horizon_world::registry::BlockStateId> {
        if self.shows == Shows::Block { self.block } else { None }
    }

    pub(in crate::game) fn hz_jade_item(&self) -> Option<&horizon_client::items::ItemStack> {
        if self.shows == Shows::Item { self.item.as_ref() } else { None }
    }
}
