from chat_service import ChatService


def main() -> int:
    service = ChatService()
    service.load()

    print("Type your message (/exit to quit).")
    while True:
        message = input("> ").strip()
        if message.lower() == "/exit":
            break

        if not message:
            continue

        reply = service.generate_reply(message)
        print(f"Bot: {reply}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
