import re, os

DATAS = r"Assets/_project/Datas"

# 메뉴: (price, cost)  — 도구 경제 반영
vals = {
    # FishCuttingTable (1종, 4초, 전용) — 프리미엄 67%
    'Sushi': (15000, 5000),
    # Fryer (1종, 전용) — 가성비 65%
    'FrenchFries': (4000, 1400),
    # IceCreamMachine (1종, 전용) — 가성비 65%
    'IceCream': (4000, 1400),
    # Microwave (4종, 30k 부여 예정, 즉시조리) — 40%
    'HotDog': (3500, 2100),
    'Burrito': (6000, 3600),
    'MeatBall': (8000, 4800),
    'Pizza': (8000, 4800),
    # Grill (9종, 2초) — 45%
    'FriedEgg': (1500, 825),
    'Bacon': (3000, 1650),
    'Waffle': (4000, 2200),
    'Omelet': (5000, 2750),
    'Burger': (5500, 3025),
    'Dumplings': (6000, 3300),
    'Taco': (7000, 3850),
    'Curry': (8000, 4400),
    'RoastedChicken': (10000, 5500),
    # BreadRack (9종, 2초) — 45%
    'Bread': (2000, 1100),
    'Cookies': (2500, 1375),
    'eggTart': (3000, 1650),
    'Sandwich': (3500, 1925),
    'GarlicBread': (4000, 2200),
    'Pudding': (5000, 2750),
    'CheeseCake': (6000, 3300),
    'ChocolateCake': (6000, 3300),
    'FruitCake': (6500, 3575),
}

def set_line(text, field, value):
    return re.sub(rf'^(  {field}: ).*$', rf'\g<1>{value}', text, flags=re.M)

changed = 0
for name, (price, cost) in vals.items():
    p = f"{DATAS}/MenuData/{name}.asset"
    if not os.path.exists(p):
        print("MISSING", name); continue
    t = open(p, encoding="utf-8").read()
    t = set_line(t, "price", price)
    t = set_line(t, "cost", cost)
    open(p, "w", encoding="utf-8", newline="\n").write(t)
    changed += 1

# Microwave 설치비 0 -> 30000
mp = f"{DATAS}/FurnitureData/CookingToolData/Microwave.asset"
t = open(mp, encoding="utf-8").read()
t = set_line(t, "purchaseCost", 30000)
open(mp, "w", encoding="utf-8", newline="\n").write(t)

print(f"menus updated: {changed}/25, Microwave purchaseCost -> 30000")
