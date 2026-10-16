"""Saffron Mart stock, directly from its pokemart source table."""
import re
from map_runtime import require


def compile_saffron(src):
    text=src.text('data/maps/SaffronCity_Mart/scripts.inc')
    match=re.search(r'^SaffronCity_Mart_Items::?\s*\n(.*?)(?=^\w+::?|\Z)',text,re.M|re.S)
    require(match,'Missing Saffron Mart item table')
    stock=re.findall(r'\.2byte (ITEM_\w+)',match[1])
    require(stock and stock[-1]=='ITEM_NONE' and 'ITEM_NONE' not in stock[:-1],'Unterminated Mart stock')
    return dict(schemaVersion=1,martStock=stock[:-1])
