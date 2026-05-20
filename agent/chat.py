import asyncio

from chat_service import ChatService


async def main_async() -> int:
    service = ChatService()
    await service.load()

    print("Type your message (/exit, exit, quit, thoát to quit).")
    try:
        while True:
            message = (await asyncio.to_thread(input, "> ")).strip()
            if message.lower() in ("/exit", "exit", "quit", "/quit", "thoát", "/thoát"):
                break

            if not message:
                continue

            reply = await service.chat(message)
            print(f"Bot: {reply}\n")
    finally:
        await service.close()

    return 0


def main() -> int:
    return asyncio.run(main_async())


if __name__ == "__main__":
    raise SystemExit(main())
