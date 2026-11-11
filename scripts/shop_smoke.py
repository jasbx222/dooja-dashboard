#!/usr/bin/env python3
"""End-to-end shop checks against a disposable Development database. Writes rows. Never point this at production."""
import base64, html as htmlmod, json, os, re, threading, urllib.error, urllib.parse, urllib.request as r, http.cookiejar
BASE = os.environ.get('DOOJA_URL', 'http://127.0.0.1:5081')
jar = http.cookiejar.CookieJar()
client = r.build_opener(r.HTTPCookieProcessor(jar))
checks = []
PNG = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==')

def req(path, method='GET', data=None, token=None, status=200, form=False, headers=None, raw=None, content_type=None):
    hdrs = dict(headers or {})
    if raw is not None:
        body = raw
        if content_type: hdrs['Content-Type'] = content_type
    elif data is not None:
        if form:
            body = urllib.parse.urlencode(data, doseq=True).encode(); hdrs['Content-Type'] = 'application/x-www-form-urlencoded'
        else:
            body = json.dumps(data, ensure_ascii=False).encode(); hdrs['Content-Type'] = 'application/json'
    else:
        body = None
    if token: hdrs['Authorization'] = 'Bearer ' + token
    try:
        response = client.open(r.Request(BASE + path, body, hdrs, method=method));         code = response.status; text = htmlmod.unescape(response.read().decode())
    except urllib.error.HTTPError as e:
        code = e.code; text = htmlmod.unescape(e.read().decode())
    assert code == status, (method, path, code, text[:800])
    checks.append(f'{method} {path}: {code}')
    try: return json.loads(text)
    except Exception: return text

def csrf(html):
    return re.search(r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"', html).group(1)

def login_admin():
    html = req('/Auth/Login')
    req('/Auth/Login', 'POST', {'Phone': '07700000000', 'Password': 'Dooja.Dev!2026', '__RequestVerificationToken': csrf(html)}, form=True)
    assert 'لوحة' in req('/') or 'ضوجة' in req('/')

login_admin()
page = req('/ShopCategories/Create'); token = csrf(page)
page = req('/ShopCategories/Save', 'POST', {'Id': '0', 'Name': 'ألعاب فيديو', 'SortOrder': '1', 'Active': 'true', '__RequestVerificationToken': token}, form=True)
assert 'تم حفظ التصنيف' in page, page[:500]
cat = re.search(r'name="Id" value="(\d+)"', page).group(1)
page = req('/ShopProducts/Create'); token = csrf(page)
assert f'value="{cat}"' in page
page = req('/ShopProducts/Save', 'POST', {'Id': '0', 'CategoryId': cat, 'Name': 'يد تحكم', 'Description': 'وصف حقيقي', 'Specs': 'لاسلكي', 'Price': '25000', 'CompareAtPrice': '30000', 'Stock': '2', 'Active': 'true', 'Featured': 'true', '__RequestVerificationToken': token}, form=True)
assert 'تم حفظ المنتج' in page, page[:800]
pid = int(re.search(r'name="Id" value="(\d+)"', page).group(1))
boundary = '----dooja'
body = (f'--{boundary}\r\nContent-Disposition: form-data; name="id"\r\n\r\n{pid}\r\n'
        f'--{boundary}\r\nContent-Disposition: form-data; name="files"; filename="px.png"\r\nContent-Type: image/png\r\n\r\n').encode() + PNG + f'\r\n--{boundary}\r\nContent-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n{csrf(page)}\r\n--{boundary}--\r\n'.encode()
uploaded = req('/ShopProducts/Upload', 'POST', raw=body, content_type='multipart/form-data; boundary=' + boundary)
assert 'تم رفع الصور' in uploaded, uploaded[:500]
public = req(f'/api/shop/products/{pid}')
assert public['price'] == 25000 and public['compareAtPrice'] == 30000 and public['discountPercent'] == 16 and public['imageUrl'].startswith('/uploads/')
assert req('/api/shop/products?q=' + urllib.parse.quote('يد'))['total'] == 1
assert req('/api/shop/products?categoryId=' + cat)['total'] == 1
assert req('/api/shop/products?sort=discount&discounted=true')['total'] >= 1
assert req('/api/shop/products?minPrice=26000')['total'] == 0
cats = req('/api/shop/categories')
assert any(c['name'] == 'ألعاب فيديو' for c in cats)
req('/api/auth/request-otp', 'POST', {'phone': '07700000001'})
user = req('/api/auth/verify', 'POST', {'phone': '07700000001', 'code': '1234'})
token_user = user['token']
cart = req('/api/cart/items', 'POST', {'productId': pid, 'qty': 5}, token=token_user, status=400)
assert '2' in cart['error']
cart = req('/api/cart/items', 'POST', {'productId': pid, 'qty': 1}, token=token_user)
assert cart['count'] == 1 and cart['subtotal'] == 25000
# Client-supplied totals are ignored.
order = req('/api/orders', 'POST', {'name': 'حسين', 'phone': '07700000001', 'governorate': 'بغداد', 'area': 'المنصور', 'address': 'شارع 1', 'notes': '', 'payMethod': 'cod', 'idempotencyKey': 'idem-shop-1', 'total': 1, 'price': 1}, token=token_user, status=201)
assert order['total'] == 25000 and order['items'][0]['unitPrice'] == 25000 and order['number'].startswith('SH-')
replay = req('/api/orders', 'POST', {'name': 'حسين', 'phone': '07700000001', 'governorate': 'بغداد', 'area': 'المنصور', 'address': 'شارع 1', 'payMethod': 'cod', 'idempotencyKey': 'idem-shop-1'}, token=token_user)
assert replay['number'] == order['number'] and replay['id'] == order['id']
assert req(f"/api/shop/products/{pid}")['stock'] == 1
req('/api/orders', 'POST', {'name': 'x', 'phone': '123', 'governorate': 'بغداد', 'area': 'المنصور', 'address': 'شارع', 'payMethod': 'cod', 'idempotencyKey': 'idem-bad-phone'}, token=token_user, status=400)
req('/api/auth/request-otp', 'POST', {'phone': '07700000003'})
other = req('/api/auth/verify', 'POST', {'phone': '07700000003', 'code': '1234'})['token']
req(f"/api/orders/{order['id']}", token=other, status=404)
req(f"/api/orders/{order['id']}/cancel", 'POST', token=other, status=400)
cancelled = req(f"/api/orders/{order['id']}/cancel", 'POST', token=token_user)
assert cancelled['status'] == 'ملغي'
assert req(f"/api/shop/products/{pid}")['stock'] == 2
# Second unit, then a concurrent duplicate must not oversell the last unit.
req('/api/cart/items', 'POST', {'productId': pid, 'qty': 1}, token=token_user)
req('/api/cart/items', 'POST', {'productId': pid, 'qty': 1}, token=other)
page = req('/ShopInventory'); token_form = csrf(page)
req('/ShopInventory/Adjust', 'POST', {'productId': str(pid), 'delta': '-1', 'note': 'تجهيز فحص التزامن', '__RequestVerificationToken': token_form}, form=True)
assert req(f"/api/shop/products/{pid}")['stock'] == 1
results = []
def buy(tok, key):
    try:
        results.append(req('/api/orders', 'POST', {'name': 'حسين', 'phone': '07700000001', 'governorate': 'بغداد', 'area': 'المنصور', 'address': 'شارع 1', 'payMethod': 'cod', 'idempotencyKey': key}, token=tok, status=201))
    except AssertionError as e:
        results.append(('fail', str(e)[:200]))
threads = [threading.Thread(target=buy, args=(token_user, 'idem-race-a')), threading.Thread(target=buy, args=(other, 'idem-race-b'))]
for t in threads: t.start()
for t in threads: t.join()
wins = [x for x in results if isinstance(x, dict)]
fails = [x for x in results if not isinstance(x, dict)]
assert len(wins) == 1 and len(fails) == 1, results
assert req(f"/api/shop/products/{pid}")['stock'] == 0
req('/api/cart/items', 'POST', {'productId': pid, 'qty': 1}, token=token_user, status=400)
# Confirmed orders cannot be cancelled by the customer. Admin can cancel and stock returns.
page = req(f"/ShopOrders/Detail/{wins[0]['id']}"); token_form = csrf(page)
page = req('/ShopOrders/Status', 'POST', {'id': wins[0]['id'], 'status': 'مؤكد', 'note': 'تم التأكيد', '__RequestVerificationToken': token_form}, form=True)
assert 'تم تحديث حالة الطلب' in page
req(f"/api/orders/{wins[0]['id']}/cancel", 'POST', token=token_user, status=400)
page = req(f"/ShopOrders/Detail/{wins[0]['id']}"); token_form = csrf(page)
page = req('/ShopOrders/Status', 'POST', {'id': wins[0]['id'], 'status': 'ملغي', 'note': 'إلغاء إداري', '__RequestVerificationToken': token_form}, form=True)
assert 'تم تحديث حالة الطلب' in page
assert req(f"/api/shop/products/{pid}")['stock'] == 1
# Price snapshot survives a later price edit.
req('/api/cart/items', 'POST', {'productId': pid, 'qty': 1}, token=token_user)
kept = req('/api/orders', 'POST', {'name': 'حسين', 'phone': '07700000001', 'governorate': 'بغداد', 'area': 'المنصور', 'address': 'شارع 1', 'payMethod': 'cod', 'idempotencyKey': 'idem-price'}, token=token_user, status=201)
page = req(f'/ShopProducts/Edit/{pid}'); token_form = csrf(page)
req('/ShopProducts/Save', 'POST', {'Id': str(pid), 'CategoryId': cat, 'Name': 'يد تحكم', 'Description': 'وصف حقيقي', 'Specs': 'لاسلكي', 'Price': '27000', 'CompareAtPrice': '30000', 'Stock': '1', 'Active': 'true', 'Featured': 'true', '__RequestVerificationToken': token_form}, form=True)
assert req(f"/api/orders/{kept['id']}", token=token_user)['items'][0]['unitPrice'] == 25000
assert req(f"/api/shop/products/{pid}")['price'] == 27000
page = req('/ShopInventory'); assert 'يد تحكم' in page and 'سجل الحركة' in page
page = req('/ShopReports'); assert 'مبيعات المتجر' in page
page = req(f"/ShopOrders/Print/{kept['id']}"); assert kept['number'] in page
page = req('/ShopProducts/Create'); token_form = csrf(page)
page = req('/ShopProducts/Save', 'POST', {'Id': '0', 'CategoryId': cat, 'Name': 'سماعة', 'Description': 'سماعة', 'Specs': 'سلكية', 'Price': '15000', 'Stock': '4', 'Active': 'true', 'Featured': 'false', '__RequestVerificationToken': token_form}, form=True)
assert 'تم حفظ المنتج' in page, page[:600]
pid2 = int(re.search(r'name="Id" value="(\d+)"', page).group(1))
req('/api/cart/items', 'POST', {'productId': pid2, 'qty': 1}, token=token_user)
merged = req('/api/cart/merge', 'POST', {'items': [{'productId': pid2, 'qty': 2}]}, token=token_user)
line = next(x for x in merged['cart']['items'] if x['productId'] == pid2)
assert line['qty'] == 3, merged
page = req('/ShopProducts'); token_form = csrf(req(f'/ShopProducts/Edit/{pid}'))
req('/ShopProducts/Archive', 'POST', {'id': str(pid), '__RequestVerificationToken': token_form}, form=True)
req(f'/api/shop/products/{pid}', status=404)
assert req(f"/api/orders/{kept['id']}", token=token_user)['items'][0]['name'] == 'يد تحكم'
# Archived products are not merged back into a sellable cart line.
merged = req('/api/cart/merge', 'POST', {'items': [{'productId': pid, 'qty': 1}]}, token=token_user)
assert all(line['productId'] != pid for line in merged['cart']['items'])
print('\n'.join(checks))
print(f'PASS: {len(checks)} shop checks')
