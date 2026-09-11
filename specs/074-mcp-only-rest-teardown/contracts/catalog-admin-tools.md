# Contract — Yeni Catalog `/mcp-admin` Tool'ları (074)

Tümü **korumalı `/mcp-admin`** ucunda (070 yol-prefix filtresi; anonim `/mcp` DEĞİŞMEZ). Yazma tool'ları
`[RequiredScope(CatalogWrite)]` handler'da + `AdminActionLog` izi. MCP tool = ince sarmalayıcı; yalnız
`Features/Agents/*ForAgent` slice'ını `IMessageBus.InvokeAsync` ile çağırır. Kullanıcı `ICurrentUser`
(HttpContext token) ile çözülür → `UserId` command'a geçer (`AdminUpdateProduct` emsali).

Ad sabitleri: `Shared.CatalogAdminTools.*`. Response tipleri Agent slice'ında (`FeatureObjectResultModel<T>`
/ `FeatureListResultModel<T>`).

## Yazma tool'ları

| Tool adı (sabit) | Girdi (MCP param) | Çağırdığı davranış | Yanıt (özet) | İz |
|---|---|---|---|---|
| `admin_create_product` | name, isbn, price, shortDescription?, fullDescription?, authorIds?/newAuthorNames?, publisherId?/newPublisherName?, categoryId?, imageUrl? | `Product.Create` (+ get-or-create author/publisher; ISBN çakışması→Error, R1) | oluşan ürün güncel künyesi | Executed/Rejected |
| `admin_set_product_dimensions` | productId, width, height, depth (+ birim) | `Product.SetDimensions` | güncel ölçüler | Executed |
| `admin_set_product_seo` | productId, metaTitle?, metaDescription?, slug? | `Product.SetSeo` | güncel SEO | Executed |
| `admin_assign_product_tag` | productId, tagId | `Product.AddTag` | güncel etiket seti | Executed |
| `admin_remove_product_tag` | productId, tagId | tag çıkar (mevcut RemoveTag mantığı) | güncel etiket seti | Executed |
| `admin_create_category` | name, description?, parentId?, seo? | `Category.Create` | oluşan kategori | Executed |
| `admin_update_category` | categoryId, name?, description?, seo? | `Category.Rename`/`SetSeo` (kısmi) | güncel kategori | Executed/Rejected |
| `admin_create_author` | name | `Author.Create` (get-or-create normalize) | oluşan yazar {id, name} | Executed |
| `admin_create_product_tag` | name | `ProductTag.Create` | oluşan etiket {id, name} | Executed |
| `admin_rename_product_tag` | tagId, name | `ProductTag.Rename` | güncel etiket | Executed/Rejected |
| `admin_create_specification_attribute` | name, filterable, displayOrder | `SpecificationAttribute.Create` | oluşan attribute {id} | Executed |
| `admin_add_specification_attribute_option` | attributeId, name, displayOrder | `SpecificationAttribute.AddOption` | oluşan option {id} | Executed/Rejected |

## Okuma (list) tool'ları — korumalı, iz YOK

| Tool adı (sabit) | Girdi | Çağırdığı slice | Yanıt |
|---|---|---|---|
| `admin_list_product_tags` | q?, page?, pageSize? | `Features/Agents` list slice | etiket listesi {id, name} |
| `admin_list_specification_attributes` | q? | `Features/Agents` list slice | attribute + option listesi |
| `admin_list_all_stock` (stock BC) | page?, pageSize? | stock `Features/Agents` list slice | ürün stok listesi {productId, onHand} |

## Kurallar

- **Param varsayılanı**: opsiyonel MCP param'a `= null`/`= default` ZORUNLU (yoksa LLM omit'te
  `ArgumentException` — bkz [[mcp-tool-optional-param-default]] emsali).
- **Kısmi güncelleme**: `admin_update_category` = yalnız verilen alan değişir (`AdminUpdateProduct` deseni).
- **Tek-kayıt**: yazma tool'ları TEK kayıt işler (toplu yok); Description'da belirtilir.
- **Description** = kanonik kullanım rehberi (070: playbook'un evi tool Description'ı).

## Değişmeyen kontratlar (korunur, referans)

- customer `/internal/payment-context`, `/internal/merchant-key` (S2S REST)
- basket `basket_items` / `basket_clear` gRPC (`Shared/Protos`)
- checkout broker komut/yanıtları (`Shared/CheckoutMessages`)
- anonim `/mcp` keşif tool seti (catalog: get_product/search_products/get_price_history/list_*)