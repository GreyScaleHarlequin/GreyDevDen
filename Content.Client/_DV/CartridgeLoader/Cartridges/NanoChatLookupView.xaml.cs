// SPDX-FileCopyrightText: 2025 BlitzTheSquishy <73762869+BlitzTheSquishy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tobias Berger <toby@tobot.dev>
// SPDX-FileCopyrightText: 2025 Will-Oliver-Br <164823659+Will-Oliver-Br@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <flyingkarii@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.Numerics;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._DV.CartridgeLoader.Cartridges;

[GenerateTypedNameReferences]
public sealed partial class NanoChatLookupView : PanelContainer
{
    public NanoChatLookupView()
    {
        RobustXamlLoader.Load(this);
    }

    public event Action<NanoChatRecipient>? OnStartChat;

    public void UpdateContactList(NanoChatUiState state)
    {
        ContactsList.RemoveAllChildren();
        if (state.Contacts is not { } contacts)
        {
            ContactsList.AddChild(new Label() { Text = Loc.GetString("nano-chat-look-up-no-server") });
            return;
        }

        for (var idx = 0; idx < contacts.Count; idx++)
        {
            var contact = contacts[idx];
            var isEvenRow = idx % 2 == 0;
            var contactControl = new ContactContainer(contact, state, isEvenRow, OnStartChat);
            ContactsList.AddChild(contactControl);
        }
    }

    public sealed class ContactContainer : PanelContainer
    {
        public ContactContainer(NanoChatRecipient contact, NanoChatUiState state, bool isEvenRow, Action<NanoChatRecipient>? onStartChat)
        {
            HorizontalExpand = true;
            StyleClasses.Add(isEvenRow ? "PanelBackgroundBaseDark" : "PanelBackgroundLight");

            var nameLabel = new Label()
            {
                Text = contact.Name,
                HorizontalAlignment = HAlignment.Left,
                HorizontalExpand = true
            };
            var numberLabel = new Label()
            {
                Text = $"#{contact.Number:D4}",
                HorizontalAlignment = HAlignment.Right,
                Margin = new Thickness(0, 0, 36, 0),
            };
            var startChatButton = new Button()
            {
                Text = "+",
                HorizontalAlignment = HAlignment.Right,
                MinSize = new Vector2(32, 32),
                MaxSize = new Vector2(32, 32),
                ToolTip = Loc.GetString("nano-chat-new-chat"),
            };
            startChatButton.AddStyleClass("OpenBoth");

            if (contact.Number == state.OwnNumber || state.Recipients.ContainsKey(contact.Number) || state.MaxRecipients <= state.Recipients.Count)
            {
                startChatButton.Disabled = true;
            }

            startChatButton.OnPressed += _ => onStartChat?.Invoke(contact);

            AddChild(nameLabel);
            AddChild(numberLabel);
            AddChild(startChatButton);
        }
    }
}
